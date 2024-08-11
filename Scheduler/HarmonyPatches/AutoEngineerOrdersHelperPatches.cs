using Game.Messages;
using HarmonyLib;
using JetBrains.Annotations;
using Model;
using Model.AI;
using UI.EngineControls;

namespace Scheduler.HarmonyPatches;

[HarmonyPatch]
public static class AutoEngineerOrdersHelperPatches {

    [PublicAPI]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AutoEngineerOrdersHelper), nameof(SendAutoEngineerCommand))]
    public static void SendAutoEngineerCommand(ref AutoEngineerMode mode, bool forward, ref int maxSpeedMph, float? distance, Car ____locomotive) {
        if (!SchedulerPlugin.Shared!.IsEnabled) {
            return;
        }
        // this is a hack - find out why locomotive stops in yard mode and tweak that ...
        if (mode == AutoEngineerMode.Road && distance != null) {
            var persistence = new AutoEngineerPersistence(____locomotive.KeyValueObject!);
            mode = AutoEngineerMode.Yard;
            maxSpeedMph = persistence.Orders.MaxSpeedMph;
        }
    }

}
