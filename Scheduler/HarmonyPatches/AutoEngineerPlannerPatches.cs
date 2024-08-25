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

    // this will make train traveling in road mode to stop after defined distance (same behavior as yard mode, but with road speed)
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AutoEngineerPlanner), nameof(HandleCommand))]
    public static void HandleCommand(AutoEngineerPlanner __instance, AutoEngineerCommand command) {
        if (command is { Mode: AutoEngineerMode.Road, Distance: not null }) {
            __instance.SetManualStopDistance(command.Distance.Value);
        }
    }
}
