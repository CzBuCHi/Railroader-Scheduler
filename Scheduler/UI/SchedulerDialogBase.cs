using System;
using GalaSoft.MvvmLight.Messaging;
using Model;
using Scheduler.Messages;
using UI.Builder;
using UI.Common;

namespace Scheduler.UI;

public abstract class SchedulerDialogBase
{
    private static SchedulerDialog? _Instance;

    protected static SchedulerDialog Instance {
        get => _Instance ??= new SchedulerDialog();
        set {
            if (_Instance != null) {
                _Instance._Window.OnShownDidChange -= WindowOnOnShownDidChange;
            }

            _Instance = value;
        }
    }

    private readonly Window _Window = SchedulerPlugin.UiHelper.CreateWindow(800, 500, Window.Position.Center);

    protected SchedulerDialogBase() {
        _Window.Title = "AI Scheduler";
        _Window.OnShownDidChange += WindowOnOnShownDidChange;
    }

    private static void WindowOnOnShownDidChange(bool isShown) {
        if (!isShown) {
            Messenger.Default.Send(new RebuildCarInspectorAIPanel());
            SchedulerPlugin.SelectedSwitch = null;
            SchedulerPlugin.ShowTrackSwitchVisualizers = false;
        }
    }

    public void ShowWindow(BaseLocomotive locomotive) {
        SchedulerPlugin.UiHelper.PopulateWindow(_Window, BuildWindow(locomotive));
        if (!_Window.IsShown) {
            _Window.ShowWindow();
        }
    }

    private Action<UIPanelBuilder> BuildWindow(BaseLocomotive locomotive) {
        return builder => BuildWindow(builder, locomotive);
    }

    protected abstract void BuildWindow(UIPanelBuilder builder, BaseLocomotive locomotive);
}
