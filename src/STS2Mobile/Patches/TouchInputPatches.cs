using System;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace STS2Mobile.Patches;

// Cancels card play when the touch is released outside the play zone.
// The desktop game relies on mouse-up position, but on mobile the drag target
// can drift below the play zone threshold during a swipe.
public static class TouchInputPatches
{
    public static void Apply(Harmony harmony)
    {
        var mouseCardPlayType = typeof(MegaCrit.Sts2.Core.Nodes.NGame).Assembly.GetType(
            "MegaCrit.Sts2.Core.Nodes.Combat.NMouseCardPlay"
        );
        if (mouseCardPlayType != null)
        {
            PatchHelper.Patch(
                harmony,
                mouseCardPlayType,
                "_Input",
                postfix: PatchHelper.Method(
                    typeof(TouchInputPatches),
                    nameof(MouseCardPlayInputPostfix)
                )
            );
        }
    }

    // On left mouse button release, check if the card is still in the play zone.
    // If not, cancel the card play to prevent accidental plays from imprecise touches.
    public static void MouseCardPlayInputPostfix(object __instance, object inputEvent)
    {
        try
        {
            var inputEvt = (InputEvent)inputEvent;
            if (
                inputEvt is InputEventMouseButton mouseBtn
                && mouseBtn.ButtonIndex == MouseButton.Left
                && mouseBtn.IsReleased()
            )
            {
                var instanceType = __instance.GetType();

                var isInPlayZone = instanceType.GetMethod(
                    "IsCardInPlayZone",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                if (isInPlayZone == null)
                    return;

                bool inPlayZone = (bool)isInPlayZone.Invoke(__instance, null);

                if (!inPlayZone)
                {
                    var cancelMethod = instanceType.GetMethod(
                        "CancelPlayCard",
                        BindingFlags.Public | BindingFlags.Instance
                    );
                    cancelMethod?.Invoke(__instance, null);
                    PatchHelper.Log("Card play cancelled: touch released below play zone");
                }
            }
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"MouseCardPlayInputPostfix: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
