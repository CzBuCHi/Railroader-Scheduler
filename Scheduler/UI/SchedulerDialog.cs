using System;
using System.Collections.Generic;
using System.Linq;
using GalaSoft.MvvmLight.Messaging;
using Model;
using Scheduler.Data;
using Scheduler.Messages;
using Scheduler.Utility;
using UI.Builder;
using UI.Common;

namespace Scheduler.UI;

public sealed class SchedulerDialog
{
    private readonly Window _Window = SchedulerPlugin.UiHelper.CreateWindow(800, 500, Window.Position.Center);

    public SchedulerDialog() {
        _Window.Title = "AI Scheduler";
        _Window.OnShownDidChange += WindowOnOnShownDidChange;
    }

    private void WindowOnOnShownDidChange(bool isShown) {
        if (!isShown) {
            Messenger.Default!.Send(new RebuildCarInspectorAIPanel());
        }
    }

    public void ShowWindow(BaseLocomotive locomotive) {
        _Locomotive = locomotive;

        SchedulerPlugin.UiHelper.PopulateWindow(_Window, BuildWindow);
        if (!_Window.IsShown) {
            _Window.ShowWindow();
        }
    }

    private BaseLocomotive _Locomotive = null!;

    private string _ScheduleName = "Schedule";
    private bool _ScheduleNameConflict;

    private void SetScheduleName(string value) {
        if (_ScheduleName == value) {
            return;
        }

        _ScheduleName = value;
        _ScheduleNameConflict = Schedules.FindIndex(o => o != SelectedSchedule && o.Name == _ScheduleName) != -1;
    }

    private List<Schedule> Schedules => SchedulerPlugin.Settings.Schedules;
    private readonly UIState<string?> _SelectedScheduleName = new(null);
    private Schedule SelectedSchedule => Schedules.First(o => o.Name == _SelectedScheduleName.Value);
    private bool _NewSchedule;
    private Schedule? _EditedSchedule;
    private bool _RenameSchedule;

    private void BuildWindow(UIPanelBuilder builder) {
        _Window.Title = _EditedSchedule == null ? "AI Scheduler" : "AI Scheduler | " + _EditedSchedule.Name;

        builder.RebuildOnEvent<RebuildSchedulePanel>();

        builder.ButtonStrip(strip => {
            if (!_NewSchedule && _EditedSchedule == null) {
                strip.AddButton("Create new", () => CreateNewSchedule(builder));

                if (_SelectedScheduleName.Value != null) {
                    strip.AddButton("Remove", () => RemoveSchedule(builder));
                    strip.AddButton("Rename", () => RenameSchedule(builder));
                    strip.AddButton("Modify", () => ModifySchedule(builder));
                }
            } else if (_NewSchedule) {
                strip.AddButton("Save", () => ConfirmCreateNewSchedule(builder));
                strip.AddButton("Cancel", () => CancelNewSchedule(builder));
            } else if (_RenameSchedule) {
                strip.AddButton("Save", () => ConfirmRenameSchedule(builder));
                strip.AddButton("Cancel", () => CancelRenameSchedule(builder));
            } else if (_EditedSchedule != null) {
                strip.AddButton("Save", () => ConfirmModifySchedule(builder));
                strip.AddButton("Cancel", () => CancelModifySchedule(builder));
            }
        });

        if (_NewSchedule || _RenameSchedule) {
            var labelText = "Schedule Name";
            if (_ScheduleNameConflict) {
                labelText = labelText.ColorRed()!;
            }

            builder.AddField(labelText, builder.AddInputField(_ScheduleName, SetScheduleName, characterLimit: 50)!);
        }

        if (_EditedSchedule != null) {
            builder.AddSection(_EditedSchedule.Name, section => BuildEditor(section, _EditedSchedule));
        } else {
            var schedules = SchedulerPlugin.Settings.Schedules;
            var listItems = schedules.Select(o => new UIPanelBuilder.ListItem<Schedule>(o.Name, o, "Saved schedules", o.Name));
            builder.AddListDetail(listItems, _SelectedScheduleName, BuildDetail);
        }

        builder.AddExpandingVerticalSpacer();
    }

    private int _CurrentCommandTypeIndex;
    private bool _NewCommand;
    private int _CurrentCommandIndex;
    private Schedule? _CurrentSchedule;

    private void BuildDetail(UIPanelBuilder builder, Schedule? schedule) {
        if (schedule == null) {
            builder.AddLabel(SchedulerPlugin.Settings.Schedules.Any() ? "Please select a schedule." : "No schedules configured.");
            return;
        }

        if (schedule != _CurrentSchedule) {
            _CurrentSchedule = schedule;
            Messenger.Default!.Send(new RebuildSchedulePanel());
        }

        BuildCommandList(builder, schedule);
    }

    private void BuildCommandList(UIPanelBuilder builder, Schedule schedule) {
        builder.VScrollView(view => {
            for (var i = 0; i < schedule.Commands.Count; i++) {
                var text = schedule.Commands[i]!.DisplayText;
                if (_EditedSchedule != null && _CurrentCommandIndex == i) {
                    text = text.ColorYellow()!;
                }

                view.AddLabel(text);
            }
        });
    }

    private void BuildEditor(UIPanelBuilder builder, Schedule schedule) {
        builder.AddLabel("Schedule: " + schedule.Name);

        builder.AddField("Commands",
            builder.ButtonStrip(strip => {
                strip.AddButton("Add", () => CreateCommand(builder));
                strip.AddButton("Remove", () => RemoveCommand(builder, schedule));
                strip.AddButton("Prev", () => PrevCommand(builder));
                strip.AddButton("Next", () => NextCommand(builder, schedule));
                strip.AddButton("Move up", () => MoveUp(builder, schedule));
                strip.AddButton("Move down", () => MoveDown(builder, schedule));
            })!
        );

        if (!_NewCommand) {
            BuildCommandList(builder, schedule);
            return;
        }

        builder.AddField("Command", builder.AddDropdown(ScheduleCommands.Commands, _CurrentCommandTypeIndex, o => PickCommandType(o, builder))!);


        var manager = ScheduleCommands.GetManager(_CurrentCommandTypeIndex);
        manager.BuildPanel(builder, _Locomotive);
        
        builder.ButtonStrip(strip => {
            strip.AddButton("Confirm", () => ConfirmCreateCommand(builder, manager, schedule));
            strip.AddButton("Cancel", () => CancelCreateCommand(builder));
        });
    }

    #region Handlers

    private void CreateNewSchedule(UIPanelBuilder builder) {
        _NewSchedule = true;
        SetScheduleName("New schedule #" + (Schedules.Count + 1));
        builder.Rebuild();
    }

    private void RemoveSchedule(UIPanelBuilder builder) {
        var index = Schedules.FindIndex(o => o.Name == _SelectedScheduleName.Value);
        Schedules.RemoveAt(index);
        if (index > 0) {
            _SelectedScheduleName.Value = Schedules[index - 1]!.Name;
        }

        builder.Rebuild();
    }

    private void RenameSchedule(UIPanelBuilder builder) {
        _RenameSchedule = true;
        builder.Rebuild();
    }

    private void ModifySchedule(UIPanelBuilder builder) {
        _EditedSchedule = SelectedSchedule.Clone();
        _CurrentCommandTypeIndex = 0;
        builder.Rebuild();
    }

    private void ConfirmCreateNewSchedule(UIPanelBuilder builder) {
        if (_ScheduleNameConflict) {
            return;
        }

        Schedules.Add(new Schedule { Name = _ScheduleName });
        _SelectedScheduleName.Value = _ScheduleName;
        _NewSchedule = false;
        builder.Rebuild();
        SchedulerPlugin.SaveSettings();
    }

    private void CancelNewSchedule(UIPanelBuilder builder) {
        _NewSchedule = false;
        builder.Rebuild();
    }

    private void ConfirmRenameSchedule(UIPanelBuilder builder) {
        if (_ScheduleNameConflict) {
            return;
        }

        SelectedSchedule.Name = _ScheduleName;
        _RenameSchedule = false;
        builder.Rebuild();
        SchedulerPlugin.SaveSettings();
    }

    private void CancelRenameSchedule(UIPanelBuilder builder) {
        _RenameSchedule = false;
        builder.Rebuild();
    }

    private void ConfirmModifySchedule(UIPanelBuilder builder) {
        var index = Schedules.FindIndex(o => o.Name == _SelectedScheduleName.Value);
        Schedules.RemoveAt(index);
        Schedules.Insert(index, _EditedSchedule!);
        _EditedSchedule = null;
        builder.Rebuild();
        SchedulerPlugin.SaveSettings();
    }

    private void CancelModifySchedule(UIPanelBuilder builder) {
        _EditedSchedule = null;
        builder.Rebuild();
    }

    private void PickCommandType(int commandIndex, UIPanelBuilder builder) {
        _CurrentCommandTypeIndex = commandIndex;
        builder.Rebuild();
    }

    private void CreateCommand(UIPanelBuilder builder) {
        _NewCommand = true;
        builder.Rebuild();
    }

    private void ConfirmCreateCommand(UIPanelBuilder builder, CommandManager manager, Schedule schedule) {
        var command = manager.CreateCommand();

        if (schedule.Commands.Count > _CurrentCommandIndex) {
            schedule.Commands.Insert(_CurrentCommandIndex + 1, command);
        } else {
            schedule.Commands.Add(command);
        }

        ++_CurrentCommandIndex;
        _NewCommand = false;
        builder.Rebuild();
    }

    private void CancelCreateCommand(UIPanelBuilder builder) {
        _NewCommand = false;
        builder.Rebuild();
    }

    private void RemoveCommand(UIPanelBuilder builder, Schedule schedule) {
        schedule.Commands.RemoveAt(_CurrentCommandIndex);
        _CurrentCommandIndex = Math.Max(0, _CurrentCommandIndex - 1);
        builder.Rebuild();
    }

    private void PrevCommand(UIPanelBuilder builder) {
        _CurrentCommandIndex = Math.Max(0, _CurrentCommandIndex - 1);
        builder.Rebuild();
    }

    private void NextCommand(UIPanelBuilder builder, Schedule schedule) {
        _CurrentCommandIndex = Math.Min(schedule.Commands.Count - 1, _CurrentCommandIndex + 1);
        builder.Rebuild();
    }

    private void MoveUp(UIPanelBuilder builder, Schedule schedule) {
        if (_CurrentCommandIndex == 0) {
            return;
        }

        (schedule.Commands[_CurrentCommandIndex], schedule.Commands[_CurrentCommandIndex - 1]) = (schedule.Commands[_CurrentCommandIndex - 1], schedule.Commands[_CurrentCommandIndex]);

        --_CurrentCommandIndex;
        builder.Rebuild();
    }

    private void MoveDown(UIPanelBuilder builder, Schedule schedule) {
        if (_CurrentCommandIndex == schedule.Commands.Count-1) {
            return;
        }

        (schedule.Commands[_CurrentCommandIndex], schedule.Commands[_CurrentCommandIndex + 1]) = (schedule.Commands[_CurrentCommandIndex + 1], schedule.Commands[_CurrentCommandIndex]);

        ++_CurrentCommandIndex;
        builder.Rebuild();
    }

    #endregion
}
