using System;
using Game.Messages;
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

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AutoEngineerPlanner), nameof(HandleCommand))]
    public static void HandleCommand(AutoEngineerPlanner __instance, AutoEngineerCommand command) {
        if (command.Mode == AutoEngineerMode.Road && command.Distance != null) {
            __instance.SetManualStopDistance(command.Distance.Value);
        }
    }
}
