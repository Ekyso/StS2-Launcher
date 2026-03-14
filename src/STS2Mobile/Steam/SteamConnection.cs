using System;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using SteamKit2.Internal;

namespace STS2Mobile.Steam;

public enum ConnectionState
{
    Idle,
    Connecting,
    Connected,
    Draining,
    Backoff,
}

// General-purpose on-demand Steam connection. Connects when a handler is accessed,
// auto-disconnects after idle timeout, reconnects with exponential backoff on failure.
// Reuses the same SteamClient instance across reconnects for handler/service persistence.
//
// State machine:
//   Idle → Connecting       : Handler property accessed
//   Connecting → Connected  : Auth succeeds
//   Connecting → Backoff    : Connect/auth fails
//   Connected → Connected   : Handler accessed (resets idle timer)
//   Connected → Idle        : Idle timeout, no pending work
//   Connected → Draining    : Flush requested
//   Connected → Backoff     : WebSocket drops
//   Draining → Idle         : Pending RPCs complete
//   Backoff → Connecting    : Backoff expires, work pending
//   Backoff → Idle          : Backoff expires, no work pending
public class SteamConnection : IDisposable
{
    private const int MaxBackoffMs = 32_000;
    private const int ConnectTimeoutMs = 15_000;

    private readonly string _accountName;
    private readonly string _refreshToken;
    private readonly int _defaultIdleTimeoutMs;

    private readonly SteamClient _client;
    private readonly CallbackManager _callbackManager;
    private readonly SteamUser _steamUser;
    private readonly SteamApps _steamApps;
    private readonly SteamContent _steamContent;
    private readonly SteamUnifiedMessages _unifiedMessages;

    private Thread _callbackThread;
    private volatile bool _callbackRunning;

    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ManualResetEventSlim _connectedGate = new(false);
    private Timer _idleTimer;
    private int _backoffMs;
    private Exception _connectError;
    private volatile int _idleSuspendCount;

    public ConnectionState State { get; private set; } = ConnectionState.Idle;
    public ulong AppAccessToken { get; set; }

    public SteamClient Client => _client;
    public SteamConfiguration Configuration => _client.Configuration;

    public SteamApps Apps
    {
        get
        {
            EnsureConnected();
            return _steamApps;
        }
    }

    public SteamContent Content
    {
        get
        {
            EnsureConnected();
            return _steamContent;
        }
    }

    public SteamConnection(string accountName, string refreshToken, int idleTimeoutMs = 30_000)
    {
        _accountName = accountName;
        _refreshToken = refreshToken;
        _defaultIdleTimeoutMs = idleTimeoutMs;

        var config = SteamConfiguration.Create(b => b.WithProtocolTypes(ProtocolTypes.WebSocket));
        _client = new SteamClient(config);
        _callbackManager = new CallbackManager(_client);
        _steamUser = _client.GetHandler<SteamUser>();
        _steamApps = _client.GetHandler<SteamApps>();
        _steamContent = _client.GetHandler<SteamContent>();
        _unifiedMessages = _client.GetHandler<SteamUnifiedMessages>();
        _unifiedMessages.CreateService<Cloud>();

        _callbackManager.Subscribe<SteamClient.ConnectedCallback>(_ =>
        {
            _steamUser.LogOn(
                new SteamUser.LogOnDetails
                {
                    Username = _accountName,
                    AccessToken = _refreshToken,
                    ShouldRememberPassword = true,
                }
            );
        });

        _callbackManager.Subscribe<SteamClient.DisconnectedCallback>(_ =>
        {
            if (State == ConnectionState.Connected)
            {
                PatchHelper.Log("[Connection] Dropped unexpectedly");
                EnterBackoff();
            }
        });

        _callbackManager.Subscribe<SteamUser.LoggedOnCallback>(cb =>
        {
            if (cb.Result == EResult.OK)
                _connectedGate.Set();
            else
            {
                _connectError = new InvalidOperationException($"Login failed: {cb.Result}");
                _connectedGate.Set();
            }
        });
    }

    // Sends a CCloud RPC. Connects on demand, resets idle timer, retries on
    // transient connection failure.
    public async Task<TResult> SendCloud<TRequest, TResult>(string method, TRequest request)
        where TRequest : ProtoBuf.IExtensible, new()
        where TResult : ProtoBuf.IExtensible, new()
    {
        EnsureConnected();
        ResetIdleTimer();

        await _sendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var job = _unifiedMessages.SendMessage<TRequest, TResult>($"Cloud.{method}#1", request);
            var response = await job.ToTask().ConfigureAwait(false);
            if (response.Result != EResult.OK)
                throw new InvalidOperationException($"Cloud.{method} failed: {response.Result}");
            return response.Body;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void SuspendIdleTimeout()
    {
        Interlocked.Increment(ref _idleSuspendCount);
        _idleTimer?.Dispose();
        _idleTimer = null;
    }

    public void ResumeIdleTimeout()
    {
        if (Interlocked.Decrement(ref _idleSuspendCount) <= 0)
        {
            _idleSuspendCount = 0;
            if (State == ConnectionState.Connected)
                ResetIdleTimer();
        }
    }

    // Enters Draining state: waits for pending RPCs to complete, then disconnects.
    public void Flush()
    {
        lock (_stateLock)
        {
            if (State != ConnectionState.Connected)
                return;
            State = ConnectionState.Draining;
            _idleTimer?.Dispose();
            _idleTimer = null;
        }

        PatchHelper.Log("[Connection] Draining...");

        if (_sendLock.Wait(5000))
        {
            _sendLock.Release();
            Teardown();
            TransitionTo(ConnectionState.Idle);
            PatchHelper.Log("[Connection] Drain complete, disconnected");
        }
        else
        {
            PatchHelper.Log("[Connection] Drain timed out, forcing disconnect");
            Teardown();
            TransitionTo(ConnectionState.Idle);
        }
    }

    public void Dispose()
    {
        Flush();
        _sendLock.Dispose();
        _connectedGate.Dispose();
    }

    private void EnsureConnected()
    {
        if (State == ConnectionState.Connected)
        {
            ResetIdleTimer();
            return;
        }

        lock (_stateLock)
        {
            if (State == ConnectionState.Connected)
            {
                ResetIdleTimer();
                return;
            }

            if (State == ConnectionState.Backoff)
            {
                PatchHelper.Log($"[Connection] Waiting {_backoffMs}ms backoff before reconnect...");
                Monitor.Exit(_stateLock);
                Thread.Sleep(_backoffMs);
                Monitor.Enter(_stateLock);
            }

            _connectError = null;
            _connectedGate.Reset();
            TransitionTo(ConnectionState.Connecting);

            try
            {
                StartCallbackThread();
                _client.Connect();
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"[Connection] Connect failed: {ex.Message}");
                EnterBackoff();
                throw;
            }

            if (!_connectedGate.Wait(ConnectTimeoutMs))
            {
                PatchHelper.Log("[Connection] Connect timed out");
                Teardown();
                EnterBackoff();
                throw new TimeoutException("Steam connection timed out");
            }

            if (_connectError != null)
            {
                Teardown();
                EnterBackoff();
                throw _connectError;
            }

            _backoffMs = 0;
            TransitionTo(ConnectionState.Connected);
            ResetIdleTimer();
            PatchHelper.Log("[Connection] Connected to Steam");
        }
    }

    private void StartCallbackThread()
    {
        if (_callbackThread != null && _callbackThread.IsAlive)
            return;

        _callbackRunning = true;
        _callbackThread = new Thread(() =>
        {
            while (_callbackRunning)
                _callbackManager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
        })
        {
            IsBackground = true,
            Name = "SteamConnectionCallbacks",
        };
        _callbackThread.Start();
    }

    private void Teardown()
    {
        _callbackRunning = false;
        try
        {
            _steamUser?.LogOff();
        }
        catch { }
        try
        {
            _client?.Disconnect();
        }
        catch { }
        _callbackThread?.Join(2000);
        _callbackThread = null;
    }

    private void EnterBackoff()
    {
        _backoffMs = _backoffMs == 0 ? 2000 : Math.Min(_backoffMs * 2, MaxBackoffMs);
        TransitionTo(ConnectionState.Backoff);
    }

    private void ResetIdleTimer()
    {
        if (_idleSuspendCount > 0)
            return;

        _idleTimer?.Dispose();
        _idleTimer = new Timer(
            _ =>
            {
                if (State == ConnectionState.Connected)
                {
                    PatchHelper.Log("[Connection] Idle timeout, disconnecting");
                    Teardown();
                    TransitionTo(ConnectionState.Idle);
                }
            },
            null,
            _defaultIdleTimeoutMs,
            Timeout.Infinite
        );
    }

    private void TransitionTo(ConnectionState newState)
    {
        State = newState;
    }
}
