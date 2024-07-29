namespace Scheduler.UI;

using System.Linq;
using global::UI.Builder;
using global::UI.Common;
using Model;
using Scheduler.Data;

public sealed class SchedulerDialog {

    private readonly Window _Window = SchedulerPlugin.UiHelper.CreateWindow(1000, 600, Window.Position.Center);

    public SchedulerDialog() {
        _Window.Title = "AI scheduler";
    }

    private bool _Populated;

    public void ShowWindow(BaseLocomotive locomotive) {
        if (!_Populated) {
            SchedulerPlugin.UiHelper.PopulateWindow(_Window, builder => BuildWindow(builder, locomotive));
            _Populated = true;
        }

        if (!_Window.IsShown) {
            _Window.ShowWindow();
        }
    }


    private void BuildWindow(UIPanelBuilder builder, BaseLocomotive locomotive) {
        SchedulerPlugin.Manager.Builder = builder;
        SchedulerPlugin.Manager.NewScheduleName = "Schedule #" + SchedulerPlugin.Manager.Schedules.Count;

        builder.AddSection("Record new schedule", section => {
            section.ButtonStrip(strip => {
                if (SchedulerPlugin.NewSchedule == null && !SchedulerPlugin.Manager.IsRecording) {
                    strip.AddButton("Start", SchedulerPlugin.Manager.StartRecording);
                } else {
                    strip.AddButton("Stop", SchedulerPlugin.Manager.StopRecording);
                }

                if (!SchedulerPlugin.Manager.IsRecording) {
                    strip.AddButton("Save", SchedulerPlugin.Manager.Save);
                    strip.AddButton("Discard", SchedulerPlugin.Manager.Discard);
                }
            });

            section.AddField("Name", section.AddInputField(SchedulerPlugin.Manager.NewScheduleName, o => SchedulerPlugin.Manager.NewScheduleName = o, characterLimit: 50)!);
        });

        var listItems = SchedulerPlugin.Manager.Schedules.Select(o => new UIPanelBuilder.ListItem<Schedule>(o.Name, o, "Saved schedules", o.Name));
        builder.AddListDetail(listItems, SchedulerPlugin.Manager.SelectedSchedule, (detail, schedule) => BuildDetail(detail, schedule, locomotive));
    }

    private void BuildDetail(UIPanelBuilder builder, Schedule? schedule, BaseLocomotive locomotive) {
        if (SchedulerPlugin.NewSchedule != null) {
            schedule = SchedulerPlugin.NewSchedule;
        }

        if (schedule == null) {
            builder.AddLabel(SchedulerPlugin.Manager.Schedules.Any() ? "Please select a schedule." : "No schedules configured.");
            return;
        }

        if (SchedulerPlugin.NewSchedule == null) {
            builder.AddField("Schedule",
                builder.ButtonStrip(strip => {
                    strip.AddButton("Execute", () => SchedulerPlugin.Manager.ExecuteSchedule(schedule, locomotive));
                    strip.AddButton("Modify", () => SchedulerPlugin.Manager.ModifySchedule(schedule));
                    strip.AddButton("Remove", () => SchedulerPlugin.Manager.RemoveSchedule(schedule));
                })!
            );
        } else {
            BuildAddCommandPanel(builder, locomotive, SchedulerPlugin.NewSchedule);
        }

        builder.VScrollView(view => {
            for (var i = 0; i < schedule.Commands.Count; i++) {
                var text = schedule.Commands[i].ToString();
                if (SchedulerPlugin.Manager.CurrentCommand == i) {
                    text = text.ColorYellow();
                }

                view.AddLabel(text);
            }
        });
    }

    private void BuildAddCommandPanel(UIPanelBuilder builder, BaseLocomotive locomotive, Schedule schedule) {
        builder.AddField("Commands",
            builder.ButtonStrip(strip => {
                strip.AddButton("Add", () => {
                    SchedulerPlugin.Manager.AddCommand = true;
                    SchedulerPlugin.Manager.CurrentCommand = null;
                    builder.Rebuild();
                });
                if (!SchedulerPlugin.Manager.AddCommand && SchedulerPlugin.Manager.CurrentCommand != null) {
                    strip.AddButton("Prev", () => SchedulerPlugin.Manager.PrevCommand());
                    strip.AddButton("Remove", () => SchedulerPlugin.Manager.RemoveCommand());
                    strip.AddButton("Next", () => SchedulerPlugin.Manager.NextCommand());
                }
            })!
        );

        if (SchedulerPlugin.Manager.AddCommand) {
            builder.AddDropdown(ScheduleCommands.Commands, SchedulerPlugin.Manager.SelectedCommandIndex, o => {
                SchedulerPlugin.Manager.SelectedCommandIndex = o;
                builder.Rebuild();
            });

            var panelBuilder = ScheduleCommands.CommandPanelBuilders[SchedulerPlugin.Manager.SelectedCommandIndex]!;
            panelBuilder.Configure(locomotive);
            panelBuilder.BuildPanel(builder);

            builder.AddButton("Confirm", () => {
                var command = panelBuilder.CreateScheduleCommand();
                schedule.Commands.Add(command);
                command.Execute(locomotive);
                SchedulerPlugin.Manager.AddCommand = false;
                builder.Rebuild();
            });
        }
    }

}