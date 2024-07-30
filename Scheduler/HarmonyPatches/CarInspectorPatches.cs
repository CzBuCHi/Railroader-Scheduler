using System.Linq;
using CarInspectorResizer.Behaviors;
using HarmonyLib;
using JetBrains.Annotations;
using Model;
using UI.Builder;
using UI.CarInspector;
using UI.Common;

namespace Scheduler.HarmonyPatches;

[PublicAPI]
[HarmonyPatch]
public static class CarInspectorPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CarInspector), "Populate")]
    public static void Populate(ref Window ____window) {
        var windowAutoHeight = ____window.gameObject!.GetComponent<CarInspectorAutoHeightBehavior>()!;
        windowAutoHeight.ExpandTab("orders", 75);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CarInspector), "PopulateAIPanel")]
    public static void PopulateAIPanel(UIPanelBuilder builder, Car ____car, ref Window ____window) {
        SchedulerPlugin.Manager.CarInspectorBuilder = builder;
        var schedules = SchedulerPlugin.Settings.Schedules.Select(o => o.Name).ToList();
        var currentSelectedIndex = 0;

        builder.AddField("Scheduler",
            builder.AddDropdown(schedules, currentSelectedIndex, o => currentSelectedIndex = o)!
        );
        builder.AddField("",
            builder.ButtonStrip(strip => {
                strip.AddButton("Execute",
                    () => SchedulerPlugin.Manager.ExecuteSchedule(
                        SchedulerPlugin.Settings.Schedules[currentSelectedIndex]!, (BaseLocomotive)____car));
                strip.AddButton("Manage", () => SchedulerPlugin.SchedulerDialog.ShowWindow((BaseLocomotive)____car));
            })!
        );
    }
}