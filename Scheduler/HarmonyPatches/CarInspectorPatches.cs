using System;
using System.Linq;
using CarInspectorResizer.Behaviors;
using HarmonyLib;
using JetBrains.Annotations;
using KeyValue.Runtime;
using Model;
using Scheduler.Messages;
using Scheduler.UI;
using UI.Builder;
using UI.CarInspector;
using UI.Common;

namespace Scheduler.HarmonyPatches;

[PublicAPI]
[HarmonyPatch]
public static class CarInspectorPatches
{
    const string SchedulerKey = "Scheduler:SelectedSchedule";

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CarInspector), "Populate")]
    public static void Populate(ref Window ____window) {
        var windowAutoHeight = ____window.gameObject.GetComponent<CarInspectorAutoHeightBehavior>()!;
        windowAutoHeight.ExpandTab("orders", 105);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CarInspector), "PopulateAIPanel")]
    public static void PopulateAIPanel(UIPanelBuilder builder, Car ____car, ref Window ____window) {
        builder.RebuildOnEvent<RebuildCarInspectorAIPanel>();

        var schedules = SchedulerPlugin.Settings.Schedules.Select(o => o.Name).ToList();
        if (schedules.Count > 0) {
            var index = ____car.KeyValueObject!.Get(SchedulerKey).IntValue;
            if (index < 0 || index >= schedules.Count) {
                ____car.KeyValueObject.Set(SchedulerKey, Value.Int(0));
            }

            builder.AddField("Scheduler",
                builder.AddDropdown(schedules,
                    ____car.KeyValueObject!.Get(SchedulerKey).IntValue,
                    o => ____car.KeyValueObject.Set(SchedulerKey, Value.Int(o))
                )!
            );
            builder.AddField("",
                builder.ButtonStrip(strip => {
                    strip.AddButton("Execute", () => {
                        SchedulerPlugin.Runner.ExecuteSchedule(SchedulerPlugin.Settings.Schedules[____car.KeyValueObject!.Get(SchedulerKey).IntValue]!, (BaseLocomotive)____car);
                    });
                    strip.AddButton("Manage", () => SchedulerDialog.Shared.ShowWindow((BaseLocomotive)____car));
                })!
            );
            builder.AddField("Current command", () => ____car.KeyValueObject["ScheduleRunner:CurrentCommand"], UIPanelBuilder.Frequency.Periodic);
        } else {
            builder.AddField("Scheduler",
                builder.ButtonStrip(strip => {
                    strip.AddButton("Manage", () => SchedulerDialog.Shared.ShowWindow((BaseLocomotive)____car));
                })!
            );
        }
    }
}
