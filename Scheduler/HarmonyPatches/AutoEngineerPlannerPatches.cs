namespace Scheduler.HarmonyPatches;

using System;
using Game.Messages;
using HarmonyLib;
using JetBrains.Annotations;
using Model.AI;

[PublicAPI]
[HarmonyPatch]
public static class AutoEngineerPlannerPatches {

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AutoEngineerPlanner), "HandleCommand")]
    public static void HandleCommand(AutoEngineerCommand command, AutoEngineerPersistence ____persistence, AutoEngineerPlanner __instance) {
        SchedulerPlugin.DebugMessage("AutoEngineerPlanner: " + command.Mode + " | " + command.Forward + " | " + command.MaxSpeedMph + " | " + command.Distance);

        if (command is { Mode: AutoEngineerMode.Road, Distance: not null }) {
            __instance.SetManualStopDistance(command.Distance.Value);
        }
    }

    [HarmonyReversePatch]
    [HarmonyPatch(typeof(AutoEngineerPlanner), "SetManualStopDistance")]
    public static void SetManualStopDistance(this AutoEngineerPlanner __instance, float distanceInMeters) {
        throw new NotImplementedException("This is a stub");
    }

}