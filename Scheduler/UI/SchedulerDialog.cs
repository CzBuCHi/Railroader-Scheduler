using System;
using System.Collections.Generic;
using System.Linq;
using GalaSoft.MvvmLight.Messaging;
using Model;
using Scheduler.Data;
using Scheduler.Messages;
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
            Messenger.Default!.Send(RebuildCarInspectorAIPanel.Instance);
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
        _ScheduleNameConflict = Schedules.FindIndex(o => o.Name == _ScheduleName) != -1;
    }

    private List<Schedule> Schedules => SchedulerPlugin.Settings.Schedules;
    private readonly UIState<string?> _SelectedScheduleName = new(null);
    private Schedule SelectedSchedule => Schedules.First(o => o.Name == _SelectedScheduleName.Value);
    private bool _NewSchedule;
    private Schedule? _EditedSchedule;

    private void BuildWindow(UIPanelBuilder builder) {
        builder.ButtonStrip(strip => {
            if (!_NewSchedule && _EditedSchedule == null) {
                strip.AddButton("Create new", () => {
                    _NewSchedule = true;
                    SetScheduleName("New schedule #" + (Schedules.Count + 1));
                    builder.Rebuild();
                });

                if (_SelectedScheduleName.Value != null) {
                    strip.AddButton("Remove", () => {
                        var index = Schedules.FindIndex(o => o.Name == _SelectedScheduleName.Value);
                        Schedules.RemoveAt(index);
                        if (index > 0) {
                            _SelectedScheduleName.Value = Schedules[index - 1]!.Name;
                        }

                        builder.Rebuild();
                    });
                    strip.AddButton("Modify", () => {
                        _EditedSchedule = SelectedSchedule.Clone();
                        _CurrentCommandIndex = 0;
                        builder.Rebuild();
                    });
                }
            } else if (_NewSchedule) {
                strip.AddButton("Continue", () => {
                    if (!_ScheduleNameConflict) {
                        Schedules.Add(new Schedule { Name = _ScheduleName });
                        _SelectedScheduleName.Value = _ScheduleName;
                        _NewSchedule = false;
                        builder.Rebuild();
                        SchedulerPlugin.SaveSettings();
                    }
                });
                strip.AddButton("Cancel", () => {
                    _NewSchedule = false;
                    builder.Rebuild();
                });
            } else if (_EditedSchedule != null) {
                strip.AddButton("Save", () => {
                    var index = Schedules.FindIndex(o => o.Name == _SelectedScheduleName.Value);
                    Schedules.RemoveAt(index);
                    Schedules.Insert(index, _EditedSchedule!);
                    _NewSchedule = false;
                    _EditedSchedule = null;
                    builder.Rebuild();
                    SchedulerPlugin.SaveSettings();
                });
                strip.AddButton("Cancel", () => {
                    _NewSchedule = false;
                    _EditedSchedule = null;
                    builder.Rebuild();
                });
            }
        });

        if (_NewSchedule) {
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

    private int _CurrentCommandIndex;
    private bool _NewCommand;

    private void BuildDetail(UIPanelBuilder builder, Schedule? schedule) {
        if (schedule == null) {
            builder.AddLabel(SchedulerPlugin.Settings.Schedules.Any() ? "Please select a schedule." : "No schedules configured.");
            return;
        }

        BuildCommandList(builder, schedule);
    }

    private void BuildCommandList(UIPanelBuilder builder, Schedule schedule) {
        builder.VScrollView(view => {
            for (var i = 0; i < schedule.Commands.Count; i++) {
                var text = schedule.Commands[i]!.ToString();
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
                strip.AddButton("Add", () => {
                    _NewCommand = true;
                    builder.Rebuild();
                });
                strip.AddButton("Remove", () => {
                    schedule.Commands.RemoveAt(_CurrentCommandIndex);
                    _CurrentCommandIndex = Math.Max(0, _CurrentCommandIndex - 1);
                    builder.Rebuild();
                });
                strip.AddButton("Prev", () => {
                    _CurrentCommandIndex = Math.Max(0, _CurrentCommandIndex - 1);
                    builder.Rebuild();
                });
                strip.AddButton("Next", () => {
                    _CurrentCommandIndex = Math.Min(schedule.Commands.Count - 1, _CurrentCommandIndex + 1);
                    builder.Rebuild();
                });
            })!
        );

        if (!_NewCommand) {
            BuildCommandList(builder, schedule);
            return;
        }

        builder.AddField("Command",
            builder.AddDropdown(ScheduleCommands.Commands, _CurrentCommandIndex, o => {
                _CurrentCommandIndex = o;
                builder.Rebuild();
            })!
        );

        var panelBuilder = ScheduleCommands.CommandPanelBuilders[_CurrentCommandIndex]!;
        panelBuilder.Configure(_Locomotive);
        panelBuilder.BuildPanel(builder);

        builder.ButtonStrip(strip => {
            strip.AddButton("Confirm", () => {
                var command = panelBuilder.CreateScheduleCommand();

                if (schedule.Commands.Count > _CurrentCommandIndex) {
                    schedule.Commands.Insert(_CurrentCommandIndex + 1, command);
                    ++_CurrentCommandIndex;
                } else {
                    schedule.Commands.Add(command);
                }

                command.Execute(_Locomotive);
                _NewCommand = false;
                builder.Rebuild();
            });
            strip.AddButton("Cancel", () => {
                _NewCommand = false;
                builder.Rebuild();
            });
        });
    }
}