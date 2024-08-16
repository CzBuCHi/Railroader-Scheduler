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
                strip.AddButton("Create new", () => CreateNewScheduleBegin(builder));

                if (_SelectedScheduleName.Value != null) {
                    strip.AddButton("Remove", () => RemoveSchedule(builder));
                    strip.AddButton("Rename", () => RenameScheduleBegin(builder));
                    strip.AddButton("Modify", () => ModifyScheduleBegin(builder));
                }
            } else if (_NewSchedule) {
                strip.AddButton("Save", () => CreateNewScheduleEnd(builder, true));
                strip.AddButton("Cancel", () => CreateNewScheduleEnd(builder, false));
            } else if (_RenameSchedule) {
                strip.AddButton("Save", () => RenameScheduleEnd(builder, true));
                strip.AddButton("Cancel", () => RenameScheduleEnd(builder, false));
            } else if (_EditedSchedule != null) {
                strip.AddButton("Save", () => ModifyScheduleEnd(builder, true));
                strip.AddButton("Cancel", () => ModifyScheduleEnd(builder, false));
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
                strip.AddButton("Add", () => CreateCommandBegin(builder));
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
            strip.AddButton("Confirm", () => CreateCommandEnd(builder, manager, schedule, true));
            strip.AddButton("Cancel", () => CreateCommandEnd(builder, manager, schedule, false));
        });
    }

    #region Handlers

    #region CreateNewSchedule

    private void CreateNewScheduleBegin(UIPanelBuilder builder) {
        _NewSchedule = true;
        SetScheduleName("New schedule #" + (Schedules.Count + 1));
        builder.Rebuild();
    }

    private void CreateNewScheduleEnd(UIPanelBuilder builder, bool success) {
        if (success) {
            if (_ScheduleNameConflict) {
                return;
            }

            Schedules.Add(new Schedule { Name = _ScheduleName });
            _SelectedScheduleName.Value = _ScheduleName;
            SchedulerPlugin.SaveSettings();
        }

        _NewSchedule = false;
        builder.Rebuild();
    }

    #endregion

    private void RemoveSchedule(UIPanelBuilder builder) {
        var index = Schedules.FindIndex(o => o.Name == _SelectedScheduleName.Value);
        Schedules.RemoveAt(index);
        if (index > 0) {
            _SelectedScheduleName.Value = Schedules[index - 1]!.Name;
        }

        builder.Rebuild();
    }

    #region RenameSchedule

    private void RenameScheduleBegin(UIPanelBuilder builder) {
        _RenameSchedule = true;
        builder.Rebuild();
    }

    private void RenameScheduleEnd(UIPanelBuilder builder, bool success) {
        if (success) {
            if (_ScheduleNameConflict) {
                return;
            }

            SelectedSchedule.Name = _ScheduleName;
            SchedulerPlugin.SaveSettings();
        }

        _RenameSchedule = false;
        builder.Rebuild();
    }

    #endregion

    #region ModifySchedule

    private void ModifyScheduleBegin(UIPanelBuilder builder) {
        _EditedSchedule = SelectedSchedule.Clone();
        _CurrentCommandTypeIndex = 0;
        builder.Rebuild();
    }

    private void ModifyScheduleEnd(UIPanelBuilder builder, bool success) {
        if (success) {
            var index = Schedules.FindIndex(o => o.Name == _SelectedScheduleName.Value);
            Schedules.RemoveAt(index);
            Schedules.Insert(index, _EditedSchedule!);
            SchedulerPlugin.SaveSettings();
        }

        _EditedSchedule = null;
        builder.Rebuild();
    }

    #endregion

    private void PickCommandType(int commandIndex, UIPanelBuilder builder) {
        _CurrentCommandTypeIndex = commandIndex;
        SchedulerPlugin.ShowTrackSwitchVisualizers = ScheduleCommands.GetManager(commandIndex).ShowTrackSwitchVisualizers;
        builder.Rebuild();
    }

    #region CreateCommand

    private void CreateCommandBegin(UIPanelBuilder builder) {
        _NewCommand = true;
        builder.Rebuild();
    }

    private void CreateCommandEnd(UIPanelBuilder builder, CommandManager manager, Schedule schedule, bool success) {
        if (success) {
            var command = manager.CreateCommand();

            if (schedule.Commands.Count > _CurrentCommandIndex) {
                schedule.Commands.Insert(_CurrentCommandIndex + 1, command);
            } else {
                schedule.Commands.Add(command);
            }

            ++_CurrentCommandIndex;
        }

        SchedulerPlugin.SelectedSwitch = null;
        SchedulerPlugin.ShowTrackSwitchVisualizers = false;
        _NewCommand = false;
        builder.Rebuild();
    }

    #endregion

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
        if (_CurrentCommandIndex == schedule.Commands.Count - 1) {
            return;
        }

        (schedule.Commands[_CurrentCommandIndex], schedule.Commands[_CurrentCommandIndex + 1]) = (schedule.Commands[_CurrentCommandIndex + 1], schedule.Commands[_CurrentCommandIndex]);

        ++_CurrentCommandIndex;
        builder.Rebuild();
    }

    #endregion
}
