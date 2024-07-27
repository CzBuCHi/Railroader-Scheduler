namespace Scheduler.UI;

using System.Collections.Generic;
using System.Linq;
using global::UI.Builder;
using global::UI.Common;
using Scheduler.Data;

public sealed class SchedulerDialog {

    private readonly Window _Window = SchedulerPlugin.UiHelper.CreateWindow(1000, 600, Window.Position.Center);

    public SchedulerDialog() {
        _Window.Title = "AI scheduler";
    }

    private bool _Populated;
    private bool _AddCommand;
    private int _SelectedCommandIndex;
    private readonly List<string> _Commands = ["Connect Air", "Release Handbrakes", "Set Handbrake", "Uncouple", "Set Switch", "Restore Switch"];
    private bool _Front;
    private bool _Normal;
    private List<string> _TrainCarsCached = null!;
    private int _SelectedTrainCar;

    public void ShowWindow() {
        _TrainCarsCached = TrainController.Shared!.SelectedTrain!.Select((o, i) => $"Car #{i} (" + o.DisplayName + ")").ToList();

        if (!_Populated) {
            SchedulerPlugin.UiHelper.PopulateWindow(_Window, BuildWindow);
            _Populated = true;
        }

        if (!_Window.IsShown) {
            _Window.ShowWindow();
        }
    }

    private void BuildWindow(UIPanelBuilder builder) {
        SchedulerPlugin.Manager.Builder = builder;
        SchedulerPlugin.Manager.NewScheduleName = "Schedule #" + SchedulerPlugin.Manager.Schedules.Count;

        builder.AddSection("Record new schedule", section => {
            section.ButtonStrip(strip => {
                if (SchedulerPlugin.Recorder == null) {
                    strip.AddButton("Start", SchedulerPlugin.Manager.Start);
                } else {
                    strip.AddButton("Stop", SchedulerPlugin.Manager.Stop);
                }

                if (!SchedulerPlugin.Manager.IsRecording) {
                    strip.AddButton("Save", SchedulerPlugin.Manager.Save);
                    strip.AddButton("Discard", SchedulerPlugin.Manager.Discard);
                }
            });

            section.AddField("Name", section.AddInputField(SchedulerPlugin.Manager.NewScheduleName, o => SchedulerPlugin.Manager.NewScheduleName = o, characterLimit: 50)!);
        });

        var listItems = SchedulerPlugin.Manager.Schedules.Select(o => new UIPanelBuilder.ListItem<Schedule>(o.Name, o, "Saved schedules", o.Name));
        builder.AddListDetail(listItems, SchedulerPlugin.Manager.SelectedSchedule, (detail, schedule) => {
            if (SchedulerPlugin.Recorder != null) {
                schedule = SchedulerPlugin.Recorder.Schedule;
            }

            if (schedule == null) {
                detail.AddLabel(SchedulerPlugin.Manager.Schedules.Any() ? "Please select a schedule." : "No schedules configured.");
            } else {
                if (SchedulerPlugin.Recorder == null) {
                    detail.ButtonStrip(strip => {
                        strip.AddButton("Execute", () => SchedulerPlugin.Manager.Execute(schedule, TrainController.Shared!.SelectedLocomotive!));
                        strip.AddButton("Remove", () => SchedulerPlugin.Manager.Remove(schedule));
                    });
                } else {
                    detail.ButtonStrip(strip => {
                        strip.AddButton("Add command", () => {
                            _AddCommand = true;
                            detail.Rebuild();
                        });
                    });

                    if (_AddCommand) {
                        detail.AddDropdown(_Commands, _SelectedCommandIndex, o => {
                            _SelectedCommandIndex = o;
                            detail.Rebuild();
                        });
                        switch (_SelectedCommandIndex) {
                            case 0: // "Connect Air",
                            case 1: // "Release Handbrakes",
                                break;
                            case 2: // "Set Handbrake",
                            case 3: // "Uncouple",
                                detail.AddDropdown(_TrainCarsCached, _SelectedTrainCar, o => {
                                    _SelectedTrainCar = o;
                                    detail.Rebuild();
                                });
                                break;
                            case 4: // "Set Switch",
                                detail.AddField("Location", detail.ButtonStrip(strip => {
                                    strip.AddButtonSelectable("Front of train", _Front, () => _Front = true);
                                    strip.AddButtonSelectable("Rear of train", !_Front, () => _Front = false);
                                })!);
                                detail.AddField("Orientation", detail.ButtonStrip(strip => {
                                    strip.AddButtonSelectable("Normal", _Normal, () => _Normal = true);
                                    strip.AddButtonSelectable("Reversed", !_Normal, () => _Normal = false);
                                })!);
                                break;
                            case 5: // "Restore Switch"
                                detail.AddField("Location", detail.ButtonStrip(strip => {
                                    strip.AddButtonSelectable("Front of train", _Front, () => _Front = true);
                                    strip.AddButtonSelectable("Rear of train", !_Front, () => _Front = false);
                                })!);
                                break;
                        }

                        detail.AddButton("Confirm", () => {
                            switch (_SelectedCommandIndex) {
                                case 0: // "Connect Air",
                                    SchedulerPlugin.Recorder.ConnectAir();
                                    break;
                                case 1: // "Release Handbrakes",
                                    SchedulerPlugin.Recorder.ReleaseHandbrakes();
                                    break;
                                case 2: // "Set Handbrake",
                                    SchedulerPlugin.Recorder.SetHandbrake(_SelectedTrainCar);
                                    break;
                                case 3: // "Uncouple",
                                    SchedulerPlugin.Recorder.Uncouple(_SelectedTrainCar);
                                    break;
                                case 4: // "Set Switch",
                                    SchedulerPlugin.Recorder.SetSwitch(_Front, _Normal);
                                    break;
                                case 5: // "Restore Switch"
                                    SchedulerPlugin.Recorder.RestoreSwitch(_Front);
                                    break;
                            }

                            _AddCommand = false;
                            detail.Rebuild();
                        });
                    }
                }

                detail.VScrollView(view => {
                    foreach (var command in schedule.Commands) {
                        view.AddLabel(command.ToString());
                    }
                });
            }
        });
    }

}