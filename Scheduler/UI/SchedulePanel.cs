using System;
using System.Linq;
using GalaSoft.MvvmLight.Messaging;
using Model;
using Scheduler.Commands;
using Scheduler.Data;
using Scheduler.Messages;
using Scheduler.Utility;
using Serilog;
using UI.Builder;

namespace Scheduler.UI;

public sealed class SchedulePanel
{
    private readonly CommandEditorPanel _EditorPanel = new();

    public Schedule Schedule { get; private set; } = null!;

    private int _CommandIndex;
    private bool _EditCommand;

    private readonly ILogger _Logger = Log.ForContext(typeof(SchedulerPlugin))!;

    public void BuildPanel(UIPanelBuilder builder, string scheduleName, BaseLocomotive locomotive) {
        builder.RebuildOnEvent<RebuildSchedulePanel>();

        if (Schedule == null! || Schedule.Name != scheduleName) {
            Schedule = SchedulerPlugin.Settings.Schedules.First(o => o.Name == scheduleName).Clone();
            _CommandIndex = 0;
            _EditCommand = false;
        }

        _Logger.Information($"BuildPanel: {Schedule.Name}, Count: {Schedule.Commands.Count}, Index: {_CommandIndex}, Edit: {_EditCommand}");

        builder.AddField("Commands",
            builder.ButtonStrip(strip => {
                var hasCommands = Schedule.Commands.Count > 0;
                var backwardDisabled = !(hasCommands && _CommandIndex > 0);
                var forwardDisabled = !(hasCommands && _CommandIndex < Schedule.Commands.Count - 1);

                strip.AddButton("Add", AddCommand);
                strip.AddButton("Remove", RemoveCommand).Disable(Schedule.Commands.Count == 0);
                strip.AddButton("Modify", ModifyCommand).Disable(Schedule.Commands.Count == 0);
                strip.AddButton("<<", GotoCommandIndex(GotoDirection.First)).Disable(backwardDisabled);
                strip.AddButton("<", GotoCommandIndex(GotoDirection.Prev)).Disable(backwardDisabled);
                strip.AddButton(">", GotoCommandIndex(GotoDirection.Next)).Disable(forwardDisabled);
                strip.AddButton(">>", GotoCommandIndex(GotoDirection.Last)).Disable(forwardDisabled);
                strip.AddButton("Move up", MoveCommand(MoveCommandDirection.Up)).Disable(backwardDisabled);
                strip.AddButton("Move down", MoveCommand(MoveCommandDirection.Down)).Disable(forwardDisabled);
            })!
        );

        if (_EditCommand) {
            builder.AddSection("Edit command", section => _EditorPanel.BuildPanel(section, locomotive, Schedule.Commands[_CommandIndex]!, OnCommandUpdated, OnCommandUpdateCanceled));
        } else {
            BuildCommandsView(builder);
        }
    }

    private void BuildCommandsView(UIPanelBuilder builder) {
        builder.VScrollView(view => {
            for (var i = 0; i < Schedule.Commands.Count; i++) {
                var text = Schedule.Commands[i]!.DisplayText;
                if (_CommandIndex == i) {
                    text = text.ColorYellow()!;
                }

                view.AddLabel(text);
            }
        });
    }

    private void AddCommand() {
        var command = new ConnectAir();
        Schedule.Commands.Add(command);
        _CommandIndex = Schedule.Commands.Count - 1;
        ModifyCommand();
    }

    private void RemoveCommand() {
        Schedule.Commands.RemoveAt(_CommandIndex);
        if (_CommandIndex > 0 && _CommandIndex >= Schedule.Commands.Count) {
            --_CommandIndex;
        }

        RebuildSchedulePanel();
    }

    private void ModifyCommand() {
        _EditCommand = true;
        RebuildSchedulePanel();
    }

    private enum GotoDirection
    {
        First,
        Prev,
        Next,
        Last
    }

    private Action GotoCommandIndex(GotoDirection direction) {
        return direction switch {
            GotoDirection.First => () => {
                _CommandIndex = 0;
                RebuildSchedulePanel();
            },
            GotoDirection.Prev => () => {
                --_CommandIndex;
                RebuildSchedulePanel();
            },
            GotoDirection.Next => () => {
                ++_CommandIndex;
                RebuildSchedulePanel();
            },
            GotoDirection.Last => () => {
                _CommandIndex = Schedule.Commands.Count - 1;
                RebuildSchedulePanel();
            },
            _ => () => { }
        };
    }

    private enum MoveCommandDirection
    {
        Up,
        Down
    }

    private Action MoveCommand(MoveCommandDirection direction) {
        return direction switch {
            MoveCommandDirection.Up => () => {
                (Schedule.Commands[_CommandIndex], Schedule.Commands[_CommandIndex - 1]) = (Schedule.Commands[_CommandIndex - 1], Schedule.Commands[_CommandIndex]);
                RebuildSchedulePanel();
            },
            MoveCommandDirection.Down => () => {
                (Schedule.Commands[_CommandIndex], Schedule.Commands[_CommandIndex + 1]) = (Schedule.Commands[_CommandIndex + 1], Schedule.Commands[_CommandIndex]);
                RebuildSchedulePanel();
            },
            _ => () => { }
        };
    }

    private void OnCommandUpdated(ICommand command) {
        Schedule.Commands[_CommandIndex] = command;
        _EditCommand = false;
        RebuildSchedulePanel();
    }

    private void OnCommandUpdateCanceled() {
        _EditCommand = false;
        RebuildSchedulePanel();
    }

    private static void RebuildSchedulePanel() {
        Messenger.Default.Send(new RebuildSchedulePanel());
    }

}
