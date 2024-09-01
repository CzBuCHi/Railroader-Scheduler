using System;
using HarmonyLib;

namespace Scheduler.HarmonyPatches;

[HarmonyPatch]
public static class CameraSelectorPatches
{
    [HarmonyReversePatch]
    [HarmonyPatch(typeof(CameraSelector), nameof(SelectCamera))]
    public static bool SelectCamera(this CameraSelector __instance, CameraSelector.CameraIdentifier cameraIdentifier) {
        throw new NotImplementedException("This is a stub");
    }
}
