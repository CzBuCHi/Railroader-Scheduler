namespace Scheduler.HarmonyPatches;

using CarInspectorResizer.Behaviors;
using global::UI.Builder;
using global::UI.CarInspector;
using global::UI.Common;
using HarmonyLib;
using JetBrains.Annotations;
using Model;

[PublicAPI]
[HarmonyPatch]
public static class CarInspectorPatches {

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CarInspector), "Populate")]
    public static void Populate(ref Window ____window) {
        var windowAutoHeight = ____window.gameObject!.GetComponent<CarInspectorAutoHeightBehavior>()!;
        windowAutoHeight.ExpandTab("orders", 30);
        windowAutoHeight.UpdateWindowHeight();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CarInspector), "PopulateAIPanel")]
    public static void PopulateAIPanel(UIPanelBuilder builder, Car ____car, ref Window ____window) {
        builder.AddField("",
            builder.ButtonStrip(strip =>
                strip.AddButton("Scheduler", () => SchedulerPlugin.SchedulerDialog.ShowWindow((BaseLocomotive)____car)))!
        );

    }

}