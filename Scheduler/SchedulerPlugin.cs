namespace Scheduler;

using HarmonyLib;
using JetBrains.Annotations;
using Railloader;
using Serilog;

[UsedImplicitly]
public sealed class SchedulerPlugin : SingletonPluginBase<SchedulerPlugin> {

    public static IModdingContext Context { get; private set; } = null!;
    public static IUIHelper UiHelper { get; private set; } = null!;

    private readonly ILogger _Logger = Log.ForContext<SchedulerPlugin>()!;

    public SchedulerPlugin(IModdingContext context, IUIHelper uiHelper) {
        Context = context;
        UiHelper = uiHelper;
    }

    public override void OnEnable() {
        _Logger.Information("OnEnable");
        var harmony = new Harmony("Scheduler");
        harmony.PatchAll();
    }

    public override void OnDisable() {
        _Logger.Information("OnDisable");
        var harmony = new Harmony("Scheduler");
        harmony.UnpatchAll();
    }

}