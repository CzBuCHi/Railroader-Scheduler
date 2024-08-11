using System;
using GalaSoft.MvvmLight.Messaging;
using Game.Events;
using HarmonyLib;
using JetBrains.Annotations;
using Railloader;
using Scheduler.Managers;
using Scheduler.UI;
using Scheduler.Visualizers;
using Serilog;
using UI.Builder;
using UnityEngine;
using ILogger = Serilog.ILogger;
using Object = UnityEngine.Object;

namespace Scheduler;

using Object = Object;

[UsedImplicitly]
public sealed class SchedulerPlugin : SingletonPluginBase<SchedulerPlugin>, IModTabHandler
{
    private const string ModIdentifier = "Scheduler";

    public static IModdingContext Context { get; private set; } = null!;
    public static IUIHelper UiHelper { get; private set; } = null!;
    internal static Settings Settings { get; private set; } = null!;
    internal static ScheduleRunner Runner { get; private set; } = null!;

    private readonly ILogger _Logger = Log.ForContext(typeof(SchedulerPlugin))!;

    public SchedulerPlugin(IModdingContext context, IUIHelper uiHelper) {
        Context = context;
        UiHelper = uiHelper;
        Settings = Context.LoadSettingsData<Settings>(ModIdentifier) ?? new Settings();
    }

    public override void OnEnable() {
        _Logger.Information("OnEnable");
        var harmony = new Harmony(ModIdentifier);
        harmony.PatchAll();

        Messenger.Default!.Register(this, new Action<MapDidLoadEvent>(OnMapDidLoad));
        Messenger.Default.Register(this, new Action<MapDidUnloadEvent>(OnMapDidUnload));

        var go = new GameObject(ModIdentifier);
        Runner = go.AddComponent<ScheduleRunner>()!;
    }

    public override void OnDisable() {
        _Logger.Information("OnDisable");
        var harmony = new Harmony(ModIdentifier);
        harmony.UnpatchAll();

        Messenger.Default!.Unregister(this);

        Object.Destroy(Runner.gameObject!);
        Runner = null!;
    }

    private void OnMapDidLoad(MapDidLoadEvent obj) {
        var go = new GameObject();
        go.AddComponent<TrackNodeVisualizer>();
    }

    private void OnMapDidUnload(MapDidUnloadEvent obj) {
        _SchedulerDialog = null;
        TrackNodeVisualizer.Shared = null!;
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
            global::UI.Console.Console.shared!.AddLine($"Debug: {message}");
        }
    }
}
