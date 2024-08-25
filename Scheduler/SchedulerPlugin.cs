using System;
using GalaSoft.MvvmLight.Messaging;
using Game.Events;
using HarmonyLib;
using JetBrains.Annotations;
using Railloader;
using Scheduler.Messages;
using Scheduler.UI;
using Scheduler.Utility;
using Scheduler.Visualizers;
using Serilog;
using Track;
using UI.Builder;
using UnityEngine;
using UnityEngine.Windows;
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

    private static void OnMapDidLoad(MapDidLoadEvent obj) {
        CreateVisualizers();
    }

    private static void OnMapDidUnload(MapDidUnloadEvent obj) {
        TrackNodeVisualizer.Shared = null!;
        SchedulerDialog.Shared = null!;
        LocationVisualizer.Shared = null!;
    }

    private static TrackNode? _SelectedSwitch;

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

    public static bool ShowTrackSwitchVisualizers { get; set; }

    public static TrackNode? SelectedSwitch {
        get => _SelectedSwitch;
        set {
            if (_SelectedSwitch == value) {
                return;
            }

            _SelectedSwitch = value;
            Messenger.Default!.Send(new SelectedSwitchChanged());
        }
    }

    private static void CreateVisualizers() {
        var go = new GameObject();
        go.AddComponent<TrackNodeVisualizer>();

        var go2 = new GameObject();
        go2.AddComponent<LocationVisualizer>();
        
        foreach (var trackNode in Graph.Shared.Nodes) {
            if (!Graph.Shared.IsSwitch(trackNode)) {
                continue;
            }

            var trackNodeVisualizer = new GameObject("TrackSwitchVisualizer");
            trackNodeVisualizer.transform.SetParent(trackNode.transform);
            trackNodeVisualizer.AddComponent<TrackSwitchVisualizer>();
        }
    }
}
