using System;
using System.Reflection;
using HarmonyLib;

namespace STS2Mobile;

// Shared utilities for applying Harmony patches with consistent error handling and logging.
public static class PatchHelper
{
    private const BindingFlags AllFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static void Patch(
        Harmony harmony,
        Type targetType,
        string methodName,
        MethodInfo prefix = null,
        MethodInfo postfix = null,
        MethodInfo transpiler = null,
        BindingFlags flags = AllFlags
    )
    {
        try
        {
            var target = targetType.GetMethod(methodName, flags);
            if (target == null)
            {
                Log($"FAILED {targetType.Name}.{methodName}: method not found");
                return;
            }
            harmony.Patch(
                target,
                prefix: prefix != null ? new HarmonyMethod(prefix) : null,
                postfix: postfix != null ? new HarmonyMethod(postfix) : null,
                transpiler: transpiler != null ? new HarmonyMethod(transpiler) : null
            );
            Log($"Patched {targetType.Name}.{methodName}");
        }
        catch (Exception ex)
        {
            Log($"FAILED {targetType.Name}.{methodName}: {ex.Message}");
        }
    }

    public static void PatchGetter(
        Harmony harmony,
        Type targetType,
        string propertyName,
        MethodInfo prefix
    )
    {
        try
        {
            var prop = targetType.GetProperty(propertyName, AllFlags);
            if (prop == null)
            {
                Log($"FAILED {targetType.Name}.{propertyName} getter: property not found");
                return;
            }
            var getter = prop.GetGetMethod(true);
            if (getter == null)
            {
                Log($"FAILED {targetType.Name}.{propertyName} getter: no getter");
                return;
            }
            harmony.Patch(getter, new HarmonyMethod(prefix));
            Log($"Patched {targetType.Name}.{propertyName} getter");
        }
        catch (Exception ex)
        {
            Log($"FAILED {targetType.Name}.{propertyName} getter: {ex.Message}");
        }
    }

    // Like Patch(), but throws on failure for security-critical patches.
    public static void PatchCritical(
        Harmony harmony,
        Type targetType,
        string methodName,
        MethodInfo prefix = null,
        MethodInfo postfix = null,
        BindingFlags flags = AllFlags
    )
    {
        var target =
            targetType.GetMethod(methodName, flags)
            ?? throw new InvalidOperationException(
                $"Critical patch failed: {targetType.Name}.{methodName} not found"
            );
        harmony.Patch(
            target,
            prefix: prefix != null ? new HarmonyMethod(prefix) : null,
            postfix: postfix != null ? new HarmonyMethod(postfix) : null
        );
        Log($"Patched {targetType.Name}.{methodName} (critical)");
    }

    public static MethodInfo Method(Type type, string name) =>
        type.GetMethod(name, BindingFlags.Public | BindingFlags.Static);

    public static event Action<string> LogEmitted;

    public static void Log(string msg)
    {
        Console.Error.WriteLine($"[STS2Mobile] {msg}");
        LogEmitted?.Invoke(msg);
    }
}
