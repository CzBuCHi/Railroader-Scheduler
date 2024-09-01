using System;
using GalaSoft.MvvmLight.Messaging;
using Model;
using Scheduler.Messages;
using Scheduler.Utility;
using UI.Builder;

namespace Scheduler.UI;

public sealed class CommandEditorPanel
{
    private ICommand? _CurrentCommand;
    private int _CommandTypeIndex;
    
    public void BuildPanel(UIPanelBuilder builder, BaseLocomotive locomotive, ICommand command, Action<ICommand> onUpdated, Action onCanceled) {
        builder.RebuildOnEvent<RebuildCommandEditorPanel>();

        if (_CurrentCommand != command) {
            _CurrentCommand = command;
            _CommandTypeIndex = ScheduleCommands.GetManagerIndex(command);
        }
        
        builder.AddField("Command",
            builder.AddDropdown(ScheduleCommands.Commands, _CommandTypeIndex, UpdateCommandTypeIndex)!
        );

        var manager = ScheduleCommands.GetManager(_CommandTypeIndex);
        manager.BuildPanel(builder, locomotive);
        SchedulerPlugin.ShowTrackSwitchVisualizers = manager.ShowTrackSwitchVisualizers;

        builder.ButtonStrip(strip => {
            strip.AddButton("Confirm", Confirm(onUpdated));
            strip.AddButton("Cancel", onCanceled);
        });
    }

    private void UpdateCommandTypeIndex(int commandTypeIndex) {
        _CommandTypeIndex = commandTypeIndex;
        RebuildCommandEditorPanel();
    }

    private Action Confirm(Action<ICommand> onUpdated) {
        return () => {
            var manager = ScheduleCommands.GetManager(_CommandTypeIndex);
            var command = manager.CreateCommand();
            onUpdated(command);
        };
    }

    private static void RebuildCommandEditorPanel() {
        Messenger.Default.Send(new RebuildCommandEditorPanel());
    }
}
