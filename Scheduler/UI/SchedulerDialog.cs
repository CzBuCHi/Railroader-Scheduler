namespace Scheduler.UI;

using System.Linq;
using global::UI.Builder;
using global::UI.Common;
using Model;
using Scheduler.Data;
using Scheduler.Managers;

public sealed class SchedulerDialog {

    private readonly Window _Window = SchedulerPlugin.UiHelper.CreateWindow(1000, 600, Window.Position.Center);

    public SchedulerDialog() {
        _Window.Title = "AI scheduler";
    }

    private bool _Populated;

    private readonly ScheduleManager _Manager = new();

    public void ShowWindow(BaseLocomotive locomotive) {
        _Manager.Locomotive = locomotive;

        if (!_Populated) {
            SchedulerPlugin.UiHelper.PopulateWindow(_Window, BuildWindow);
            _Populated = true;
        }

        if (!_Window.IsShown) {
            _Window.ShowWindow();
        }
    }

    private void BuildWindow(UIPanelBuilder builder) {
        _Manager.Builder = builder;
        _Manager.NewScheduleName = "Schedule #" + _Manager.Schedules.Count;

        builder.AddSection("Record new schedule", section => {
            section.ButtonStrip(strip => {
                if (!_Manager.IsRecording) {
                    if (_Manager.NewSchedule != null) {
                        strip.AddButton("Continue", _Manager.Continue);
                    } else {
                        strip.AddButton("Start", _Manager.Start);
                    }
                } else {
                    strip.AddButton("Stop", _Manager.Stop);
                }

                if (_Manager.NewSchedule != null) {
                    strip.AddButton("Save", _Manager.Save);
                    strip.AddButton("Discard", _Manager.Discard);
                }
            });

            section.AddField("Name", section.AddInputField(_Manager.NewScheduleName, o => _Manager.NewScheduleName = o, characterLimit: 50)!);
        });

        builder.AddListDetail(_Manager.Schedules.Select(GetScheduleDataItem), _Manager.SelectedSchedule, (detail, schedule) => {
            if (_Manager.NewSchedule != null) {
                schedule = _Manager.NewSchedule!;
            }

            if (schedule == null) {
                detail.AddLabel(_Manager.Schedules.Any() ? "Please select a schedule." : "No schedules configured.");
            } else {
                if (_Manager.NewSchedule == null) {
                    detail.ButtonStrip(strip => {
                        strip.AddButton("Execute", () => _Manager.Execute(schedule));
                        strip.AddButton("Remove", () => _Manager.Remove(schedule));
                    });
                }

                detail.VScrollView(view => {
                    foreach (var command in schedule.Commands) {
                        view.AddLabel(command.ToString());
                    }
                });
            }
        });
        return;

        UIPanelBuilder.ListItem<Schedule> GetScheduleDataItem(Schedule data) {
            return new UIPanelBuilder.ListItem<Schedule>(data.Name, data, "Saved schedules", data.Name);
        }
    }

}