using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using STS2Mobile.Launcher.Components;

namespace STS2Mobile.Launcher;

// Compiles shaders on first launch by collecting materials from resources and scenes,
// rendering them in a SubViewport, then writing a version marker to skip on future launches.
public class ShaderWarmupScreen : Control
{
    private const int WarmupVersion = 5;
    private const int BatchSize = 8;

    private TaskCompletionSource<bool> _tcs;
    private float _scale;
    private Label _statusLabel;
    private Label _detailLabel;
    private ProgressBar _progressBar;

    public static bool NeedsWarmup()
    {
        try
        {
            var markerPath = Path.Combine(OS.GetUserDataDir(), "shader_warmup_version");
            if (File.Exists(markerPath))
            {
                var content = File.ReadAllText(markerPath).Trim();
                if (content == WarmupVersion.ToString())
                {
                    PatchHelper.Log(
                        $"[ShaderWarmup] NeedsWarmup=false (marker v{content} matches)"
                    );
                    return false;
                }
                PatchHelper.Log(
                    $"[ShaderWarmup] NeedsWarmup=true (marker v{content} != v{WarmupVersion})"
                );
            }
            else
            {
                PatchHelper.Log("[ShaderWarmup] NeedsWarmup=true (no marker file)");
            }

            return true;
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[ShaderWarmup] NeedsWarmup check failed: {ex.Message}");
            return true;
        }
    }

    public Task WaitForCompletion()
    {
        _tcs = new TaskCompletionSource<bool>();
        return _tcs.Task;
    }

    public void Initialize()
    {
        ZIndex = 100;

        try
        {
            var vpSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            SetAnchorsPreset(LayoutPreset.FullRect);
            Size = vpSize;
            BuildUI();
            PatchHelper.Log("[ShaderWarmup] Screen initialized");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[ShaderWarmup] BuildUI failed: {ex}");
            _tcs?.TrySetResult(false);
            return;
        }

        Callable.From(RunWarmup).CallDeferred();
    }

    private void BuildUI()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var vpSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
        _scale = Math.Max(vpSize.X, vpSize.Y) / 960f;

        var bg = new ScreenBackground();
        AddChild(bg);

        var panel = new StyledPanel(_scale, widthRatio: 0.5f);
        panel.UpdateSizeFromViewport(vpSize);
        AddChild(panel);

        _statusLabel = new StyledLabel("Compiling shaders...", _scale, fontSize: 20);
        panel.Content.AddChild(_statusLabel);

        _progressBar = new StyledProgressBar(_scale);
        _progressBar.MinValue = 0;
        _progressBar.MaxValue = 100;
        _progressBar.Value = 0;
        _progressBar.ShowPercentage = true;
        panel.Content.AddChild(_progressBar);

        _detailLabel = new StyledLabel("Enumerating resources...", _scale, fontSize: 12);
        _detailLabel.Modulate = new Color(0.7f, 0.7f, 0.7f);
        panel.Content.AddChild(_detailLabel);
    }

    private async void RunWarmup()
    {
        var sw = Stopwatch.StartNew();

        try
        {
            _statusLabel.Text = "Scanning for shaders...";
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

            var materials = await CollectMaterialsAsync();
            PatchHelper.Log($"[ShaderWarmup] Collected {materials.Count} materials to warm");

            _statusLabel.Text = "Compiling shaders...";

            if (materials.Count == 0)
            {
                WriteVersionMarker();
                _tcs?.TrySetResult(true);
                return;
            }

            var viewport = new SubViewport();
            viewport.Size = new Vector2I(64, 64);
            viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            viewport.TransparentBg = true;
            AddChild(viewport);

            var whiteImage = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            whiteImage.SetPixel(0, 0, Colors.White);
            var whiteTex = ImageTexture.CreateFromImage(whiteImage);

            int processed = 0;
            int total = materials.Count;

            for (int i = 0; i < total; i += BatchSize)
            {
                var batchNodes = new List<Node>();
                int batchEnd = Math.Min(i + BatchSize, total);

                for (int j = i; j < batchEnd; j++)
                {
                    var (path, mat) = materials[j];
                    try
                    {
                        Node node = CreateWarmupNode(mat, whiteTex);
                        if (node != null)
                        {
                            viewport.AddChild(node);
                            batchNodes.Add(node);
                        }
                    }
                    catch (Exception ex)
                    {
                        PatchHelper.Log(
                            $"[ShaderWarmup] Failed to create node for {path}: {ex.Message}"
                        );
                    }
                }

                processed = batchEnd;
                double pct = 50 + (double)processed / total * 50;
                _progressBar.Value = pct;
                _detailLabel.Text = $"Compiling {processed} / {total}";

                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                foreach (var node in batchNodes)
                    node.QueueFree();
            }

            viewport.QueueFree();

            _progressBar.Value = 100;
            _statusLabel.Text = "Done!";
            _detailLabel.Text = $"Compiled {total} shaders in {sw.ElapsedMilliseconds}ms";
            PatchHelper.Log(
                $"[ShaderWarmup] Completed: {total} materials in {sw.ElapsedMilliseconds}ms"
            );

            WriteVersionMarker();

            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[ShaderWarmup] Failed: {ex}");
        }

        _tcs?.TrySetResult(true);
    }

    private static Node CreateWarmupNode(Material mat, ImageTexture whiteTex)
    {
        if (mat is ParticleProcessMaterial particleMat)
        {
            var particles = new GpuParticles2D();
            particles.ProcessMaterial = particleMat;
            particles.Amount = 1;
            particles.Emitting = true;
            particles.OneShot = false;
            particles.Texture = whiteTex;
            return particles;
        }

        var sprite = new Sprite2D();
        sprite.Texture = whiteTex;
        sprite.Material = mat;
        return sprite;
    }

    private async Task<List<(string path, Material mat)>> CollectMaterialsAsync()
    {
        var materials = new Dictionary<string, Material>();

        CollectFromDirectory("res://", materials);
        PatchHelper.Log(
            $"[ShaderWarmup] Found {materials.Count} materials from loose resource files"
        );
        _detailLabel.Text = $"Found {materials.Count} materials...";
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var scenePaths = new List<string>();
        CollectScenePaths("res://scenes", scenePaths);
        PatchHelper.Log($"[ShaderWarmup] Found {scenePaths.Count} scenes to scan");

        for (int i = 0; i < scenePaths.Count; i++)
        {
            try
            {
                var packed = ResourceLoader.Load<PackedScene>(
                    scenePaths[i],
                    null,
                    ResourceLoader.CacheMode.Reuse
                );
                if (packed != null)
                    ExtractMaterialsFromSceneState(packed, scenePaths[i], materials);
            }
            catch (Exception ex)
            {
                PatchHelper.Log(
                    $"[ShaderWarmup] Failed to extract from {scenePaths[i]}: {ex.Message}"
                );
            }

            if (i % 50 == 0)
            {
                _detailLabel.Text = $"Scanning scenes... {i} / {scenePaths.Count}";
                _progressBar.Value = (double)i / scenePaths.Count * 50;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }

        // Deduplicate by shader since many materials share the same program with different params.
        var unique = new Dictionary<string, (string path, Material mat)>();
        foreach (var (path, mat) in materials)
        {
            var shaderKey = GetShaderKey(mat);
            unique.TryAdd(shaderKey, (path, mat));
        }

        PatchHelper.Log(
            $"[ShaderWarmup] {materials.Count} total materials, {unique.Count} unique shaders"
        );
        return unique.Values.ToList();
    }

    private static string GetShaderKey(Material mat)
    {
        if (mat is ShaderMaterial sm && sm.Shader != null)
            return sm.Shader.ResourcePath ?? sm.Shader.GetRid().ToString();
        if (mat is ParticleProcessMaterial)
            return $"particle#{mat.GetRid()}";
        return mat.ResourcePath ?? mat.GetRid().ToString();
    }

    private void CollectFromDirectory(string dirPath, Dictionary<string, Material> materials)
    {
        try
        {
            using var dir = DirAccess.Open(dirPath);
            if (dir == null)
                return;

            dir.ListDirBegin();
            string fileName;
            while ((fileName = dir.GetNext()) != "")
            {
                if (fileName == "." || fileName == "..")
                    continue;

                var fullPath = $"{dirPath}/{fileName}";

                if (dir.CurrentIsDir())
                {
                    if (fileName == "debug")
                        continue;
                    CollectFromDirectory(fullPath, materials);
                    continue;
                }

                var cleanName = fileName.Replace(".remap", "");
                var cleanPath = $"{dirPath}/{cleanName}";

                if (
                    !cleanName.EndsWith(".tres")
                    && !cleanName.EndsWith(".gdshader")
                    && !cleanName.EndsWith(".material")
                )
                    continue;

                if (materials.ContainsKey(cleanPath))
                    continue;

                try
                {
                    if (!ResourceLoader.Exists(cleanPath))
                        continue;

                    // Load .tres with type hints to avoid errors from non-material resources.
                    if (cleanName.EndsWith(".tres"))
                    {
                        var mat =
                            ResourceLoader.Load(
                                cleanPath,
                                "Material",
                                ResourceLoader.CacheMode.Reuse
                            ) as Material;
                        if (mat != null)
                            materials[cleanPath] = mat;
                        else
                        {
                            var shader =
                                ResourceLoader.Load(
                                    cleanPath,
                                    "Shader",
                                    ResourceLoader.CacheMode.Reuse
                                ) as Shader;
                            if (shader != null)
                            {
                                var shaderMat = new ShaderMaterial();
                                shaderMat.Shader = shader;
                                materials[cleanPath] = shaderMat;
                            }
                        }
                        continue;
                    }

                    var res = ResourceLoader.Load(cleanPath, null, ResourceLoader.CacheMode.Reuse);
                    if (res is Material resMat)
                    {
                        materials[cleanPath] = resMat;
                    }
                    else if (res is Shader resShader)
                    {
                        var shaderMat = new ShaderMaterial();
                        shaderMat.Shader = resShader;
                        materials[cleanPath] = shaderMat;
                    }
                }
                catch (Exception ex)
                {
                    PatchHelper.Log($"[ShaderWarmup] Failed to load {cleanPath}: {ex.Message}");
                }
            }
            dir.ListDirEnd();
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[ShaderWarmup] Failed to enumerate {dirPath}: {ex.Message}");
        }
    }

    private static void CollectScenePaths(string dirPath, List<string> paths)
    {
        try
        {
            using var dir = DirAccess.Open(dirPath);
            if (dir == null)
                return;

            dir.ListDirBegin();
            string fileName;
            while ((fileName = dir.GetNext()) != "")
            {
                if (fileName == "." || fileName == "..")
                    continue;

                var fullPath = $"{dirPath}/{fileName}";

                if (dir.CurrentIsDir())
                {
                    if (fileName == "debug")
                        continue;
                    CollectScenePaths(fullPath, paths);
                    continue;
                }

                var cleanName = fileName.Replace(".remap", "");
                if (!cleanName.EndsWith(".tscn"))
                    continue;

                var cleanPath = $"{dirPath}/{cleanName}";
                if (ResourceLoader.Exists(cleanPath))
                    paths.Add(cleanPath);
            }
            dir.ListDirEnd();
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[ShaderWarmup] Failed to enumerate {dirPath}: {ex.Message}");
        }
    }

    private static void ExtractMaterialsFromSceneState(
        PackedScene packed,
        string scenePath,
        Dictionary<string, Material> materials
    )
    {
        var state = packed.GetState();
        int nodeCount = state.GetNodeCount();

        for (int n = 0; n < nodeCount; n++)
        {
            int propCount = state.GetNodePropertyCount(n);
            for (int p = 0; p < propCount; p++)
            {
                var propName = state.GetNodePropertyName(n, p).ToString();
                if (
                    propName != "material"
                    && propName != "process_material"
                    && propName != "surface_material_override/0"
                )
                    continue;

                try
                {
                    var val = state.GetNodePropertyValue(n, p);
                    if (val.Obj is Material mat)
                    {
                        var key = $"{scenePath}#node{n}#{propName}";
                        materials.TryAdd(key, mat);
                    }
                    else if (val.Obj is Shader shader)
                    {
                        var shaderMat = new ShaderMaterial();
                        shaderMat.Shader = shader;
                        var key = $"{scenePath}#node{n}#{propName}";
                        materials.TryAdd(key, shaderMat);
                    }
                }
                catch (Exception ex)
                {
                    PatchHelper.Log(
                        $"[ShaderWarmup] Failed to read property {propName} in {scenePath}: {ex.Message}"
                    );
                }
            }
        }
    }

    private static void WriteVersionMarker()
    {
        try
        {
            var markerPath = Path.Combine(OS.GetUserDataDir(), "shader_warmup_version");
            File.WriteAllText(markerPath, WarmupVersion.ToString());
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[ShaderWarmup] Failed to write version marker: {ex.Message}");
        }
    }
}
