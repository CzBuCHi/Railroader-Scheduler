namespace Scheduler;

using System;
using GalaSoft.MvvmLight.Messaging;
using Game.Events;
using global::UI.Builder;
using HarmonyLib;
using JetBrains.Annotations;
using Railloader;
using Scheduler.Data;
using Scheduler.Managers;
using Scheduler.UI;
using UnityEngine;
using Object = UnityEngine.Object;

[UsedImplicitly]
public sealed class SchedulerPlugin : SingletonPluginBase<SchedulerPlugin>, IModTabHandler {

    private const string ModIdentifier = "Scheduler";

    public static IModdingContext Context { get; private set; } = null!;
    public static IUIHelper UiHelper { get; private set; } = null!;
    internal static Settings Settings { get; private set; } = null!;
    internal static ScheduleManager Manager { get; private set; } = null!;
    internal static Schedule? NewSchedule { get; set; }

    private readonly Serilog.ILogger _Logger = Serilog.Log.ForContext(typeof(SchedulerPlugin))!;

    public SchedulerPlugin(IModdingContext context, IUIHelper uiHelper) {
        Context = context;
        UiHelper = uiHelper;
        Settings = Context.LoadSettingsData<Settings>(ModIdentifier) ?? new Settings();
    }

    public override void OnEnable() {
        _Logger.Information("OnEnable");
        var harmony = new Harmony(ModIdentifier);
        harmony.PatchAll();

        Messenger.Default!.Register(this, new Action<MapDidUnloadEvent>(OnMapDidUnload));

        var go = new GameObject(ModIdentifier);
        Manager = go.AddComponent<ScheduleManager>()!;
        NewSchedule = null;
    }

    public override void OnDisable() {
        _Logger.Information("OnDisable");
        var harmony = new Harmony(ModIdentifier);
        harmony.UnpatchAll();

        Messenger.Default!.Unregister(this);

        Object.Destroy(Manager.gameObject!);
        Manager = null!;
    }

    private void OnMapDidUnload(MapDidUnloadEvent obj) {
        _SchedulerDialog = null;
    }

    private static SchedulerDialog? _SchedulerDialog;
    public static SchedulerDialog SchedulerDialog => _SchedulerDialog ??= new SchedulerDialog();

    public void ModTabDidOpen(UIPanelBuilder builder) {
        builder
            .AddField("Debug messages",
                builder.AddToggle(() => Settings.Debug, o => Settings.Debug = o)!
            )!
            .Tooltip("Debug messages", "Send debug messages to console");
    }

    public void ModTabDidClose() {
        SaveSettings();
    }

    public static void SaveSettings() {
        Context.SaveSettingsData(ModIdentifier, Settings);
    }

    public static void DebugMessage(string message) {
        if (Settings.Debug) {
            global::UI.Console.Console.shared!.AddLine($"AI Engineer: {message}");
        }
    }

}