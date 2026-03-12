using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace STS2Mobile.Patches;

// Replaces ModelDb.Init() with a two-phase initialization to avoid circular dependency
// crashes. Phase 1 pre-populates the registry with uninitialized objects so cross-type
// references resolve during construction. Phase 2 runs the actual constructors.
public static class ModelDbInitPatch
{
    private static bool _suppressContains = false;

    public static void Apply(Harmony harmony)
    {
        PatchHelper.Patch(
            harmony,
            typeof(ModelDb),
            "Init",
            prefix: PatchHelper.Method(typeof(ModelDbInitPatch), nameof(InitPrefix))
        );
    }

    public static bool ContainsPrefix(ref bool __result)
    {
        if (_suppressContains)
        {
            __result = false;
            return false;
        }
        return true;
    }

    public static bool InitPrefix()
    {
        PatchHelper.Log("Running patched ModelDb.Init()");

        var modelDbType = typeof(ModelDb);

        var allSubtypesProp = modelDbType.GetProperty(
            "AllAbstractModelSubtypes",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
        );
        var types = (Type[])allSubtypesProp.GetValue(null);

        var getIdMethod = modelDbType.GetMethod(
            "GetId",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(Type) },
            null
        );

        var contentByIdField = modelDbType.GetField(
            "_contentById",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        var contentById = contentByIdField.GetValue(null);

        var dictType = contentById.GetType();
        var setItemMethod = dictType.GetMethod("set_Item");

        // Phase 1: Pre-populate dictionary with uninitialized objects
        PatchHelper.Log(
            $"Phase 1: Pre-registering {types.Length} types with uninitialized objects"
        );

        var typeObjects = new Dictionary<Type, object>();
        int preRegCount = 0;

        for (int i = 0; i < types.Length; i++)
        {
            try
            {
                var type = types[i];
                var id = getIdMethod.Invoke(null, new object[] { type });
                var model = RuntimeHelpers.GetUninitializedObject(type);
                setItemMethod.Invoke(contentById, new[] { id, model });
                typeObjects[type] = model;
                preRegCount++;
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"Phase 1 - Failed to pre-register {types[i].Name}: {ex.Message}");
            }
        }

        PatchHelper.Log($"Phase 1 complete: {preRegCount} types pre-registered");

        // Temporarily suppress Contains() during Phase 2 so constructors don't
        // short-circuit when they check if their type is already registered.
        var harmony = new Harmony("com.sts2mobile.modeldb");
        var containsMethod = modelDbType.GetMethod(
            "Contains",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(Type) },
            null
        );
        var containsPrefix = typeof(ModelDbInitPatch).GetMethod(
            nameof(ContainsPrefix),
            BindingFlags.Public | BindingFlags.Static
        );
        harmony.Patch(containsMethod, new HarmonyMethod(containsPrefix));

        // Phase 2: Run constructors on pre-allocated objects
        PatchHelper.Log("Phase 2: Running constructors");

        _suppressContains = true;

        int successCount = 0;
        var failed = new List<Type>();

        foreach (var type in types)
        {
            if (!typeObjects.ContainsKey(type))
                continue;

            try
            {
                RuntimeHelpers.RunClassConstructor(type.TypeHandle);

                var ctor = type.GetConstructor(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null
                );
                if (ctor != null)
                {
                    ctor.Invoke(typeObjects[type], null);
                }

                successCount++;
            }
            catch (Exception ex)
            {
                failed.Add(type);
                var inner = ex;
                while (inner.InnerException != null)
                    inner = inner.InnerException;
                PatchHelper.Log(
                    $"Phase 2 - Failed {type.Name}: {inner.GetType().Name}: {inner.Message}"
                );
            }
        }

        _suppressContains = false;
        harmony.Unpatch(containsMethod, containsPrefix);

        if (failed.Count > 0)
        {
            PatchHelper.Log(
                $"WARNING: {failed.Count}/{types.Length} types had constructor errors:"
            );
            foreach (var type in failed)
                PatchHelper.Log($"  - {type.FullName}");
        }
        else
        {
            PatchHelper.Log($"All {successCount} model types registered successfully");
        }

        return false;
    }
}
