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
    [HarmonyPatch(typeof(CarInspector), "Awake")]
    public static void Awake(ref Window ____window) {
        var windowAutoHeight = ____window.gameObject!.GetComponent<CarInspectorAutoHeightBehavior>()!;
        windowAutoHeight.ExpandTab("orders", 30);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CarInspector), "PopulateAIPanel")]
    public static void PopulateAIPanel(UIPanelBuilder builder, Car ____car) {
        builder.AddField("",
            builder.ButtonStrip(strip => strip.AddButton("Scheduler", () => SchedulerPlugin.SchedulerDialog.ShowWindow(____car)))!
        );
    }

}