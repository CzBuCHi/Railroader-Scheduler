using System;
using HarmonyLib;
using JetBrains.Annotations;
using Model.AI;

namespace Scheduler.HarmonyPatches;

[PublicAPI]
[HarmonyPatch]
public static class AutoEngineerPlannerPatches
{
    [HarmonyReversePatch]
    [HarmonyPatch(typeof(AutoEngineerPlanner), nameof(SetManualStopDistance))]
    public static void SetManualStopDistance(this AutoEngineerPlanner __instance, float distanceInMeters) {
        throw new NotImplementedException("This is a stub");
    }
}