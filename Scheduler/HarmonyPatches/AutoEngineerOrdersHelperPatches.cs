namespace Scheduler.HarmonyPatches;

using Game.Messages;
using global::UI.EngineControls;
using HarmonyLib;
using JetBrains.Annotations;
using Model.AI;

[HarmonyPatch]
public static class AutoEngineerOrdersHelperPatches {

    [UsedImplicitly]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AutoEngineerOrdersHelper), "SetOrdersValue")]
    public static void SetOrdersValue(AutoEngineerMode? mode, bool? forward, int? maxSpeedMph, float? distance, AutoEngineerPersistence ____persistence) {
        if (SchedulerPlugin.Shared?.IsEnabled != true || SchedulerPlugin.Recorder == null) {
            return;
        }

        SchedulerPlugin.Recorder.Move(
            forward ?? ____persistence.Orders.Forward,
            mode == AutoEngineerMode.Road ? maxSpeedMph ?? ____persistence.Orders.MaxSpeedMph : null,
            distance ?? 0
        );
    }

}