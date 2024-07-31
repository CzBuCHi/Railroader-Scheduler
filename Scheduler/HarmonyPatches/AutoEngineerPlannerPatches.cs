using System;
using Game.Messages;
using HarmonyLib;
using JetBrains.Annotations;
using Model.AI;
using UI.EngineControls;

namespace Scheduler.HarmonyPatches;

[PublicAPI]
[HarmonyPatch]
public static class AutoEngineerPlannerPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AutoEngineerOrdersExtensions), "MaxSpeedMph")]
    public static bool MaxSpeedMph(this AutoEngineerMode mode, ref int __result) {
        __result = mode != AutoEngineerMode.Off ? 45 : 0;
        return false;
    }
    
    [HarmonyReversePatch]
    [HarmonyPatch(typeof(AutoEngineerPlanner), "SetManualStopDistance")]
    public static void SetManualStopDistance(this AutoEngineerPlanner __instance, float distanceInMeters) {
        throw new NotImplementedException("This is a stub");
    }
}
