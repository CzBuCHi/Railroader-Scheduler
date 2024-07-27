namespace Scheduler;

using global::UI.Builder;
using HarmonyLib;
using JetBrains.Annotations;
using Railloader;
using Scheduler.UI;

[UsedImplicitly]
public sealed class SchedulerPlugin : SingletonPluginBase<SchedulerPlugin>, IModTabHandler {

    private const string ModIdentifier = "Scheduler";

    public static IModdingContext Context { get; private set; } = null!;
    public static IUIHelper UiHelper { get; private set; } = null!;
    public static Settings Settings { get; private set; } = null!;

    private readonly Serilog.ILogger _Logger = Serilog.Log.ForContext<SchedulerPlugin>()!;

    public SchedulerPlugin(IModdingContext context, IUIHelper uiHelper) {
        Context = context;
        UiHelper = uiHelper;
        Settings = Context.LoadSettingsData<Settings>(ModIdentifier) ?? new Settings();
    }

    public override void OnEnable() {
        _Logger.Information("OnEnable");
        var harmony = new Harmony(ModIdentifier);
        harmony.PatchAll();
    }

    public override void OnDisable() {
        _Logger.Information("OnDisable");
        var harmony = new Harmony(ModIdentifier);
        harmony.UnpatchAll();
    }

    private static SchedulerDialog? _SchedulerDialog;
    public static SchedulerDialog SchedulerDialog => _SchedulerDialog ??= new SchedulerDialog();

    public void ModTabDidOpen(UIPanelBuilder builder) {
    }

    public void ModTabDidClose() {
        Context.SaveSettingsData(ModIdentifier, Settings);
    }

}