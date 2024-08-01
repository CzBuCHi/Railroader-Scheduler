using System.Linq;
using Model;
using Scheduler.Data;
using Scheduler.Managers;
using UI.Builder;
using UI.Common;

namespace Scheduler.UI;

public sealed class SchedulerDialog
{
    private readonly Window _Window = SchedulerPlugin.UiHelper.CreateWindow(800, 500, Window.Position.Center);

    public SchedulerDialog() {
        _Window.Title = "AI Scheduler";
    }

    public void ShowWindow(BaseLocomotive locomotive) {
        SchedulerPlugin.UiHelper.PopulateWindow(_Window, builder => BuildWindow(builder, locomotive));
        SchedulerPlugin.Manager.Cleanup();

        if (!_Window.IsShown) {
            _Window.ShowWindow();
        }
    }

    private static void BuildWindow(UIPanelBuilder builder, BaseLocomotive locomotive) {
        builder.RebuildOnEvent<ScheduleRebuildMessage>();

        builder.ButtonStrip(strip => {
            if (!SchedulerPlugin.Manager.EditingSchedule) {
                strip.AddButton("Create new schedule", SchedulerPlugin.Manager.CreateNewSchedule);
            } else {
                var saveLabel = SchedulerPlugin.Manager.EditingExistingSchedule ? "Save changes" : "Save as new";
                strip.AddButton(saveLabel, SchedulerPlugin.Manager.SaveSchedule);
                strip.AddButton("Discard changes", SchedulerPlugin.Manager.DiscardSchedule);
            }
        });

        if (SchedulerPlugin.Manager.CurrentSchedule != null) {
            var labelText = "Schedule Name";
            if (SchedulerPlugin.Manager.ScheduleNameConflict) {
                labelText = labelText.ColorRed()!;
            }

            builder.AddField(labelText, builder.AddInputField(SchedulerPlugin.Manager.ScheduleName, o => SchedulerPlugin.Manager.ScheduleName = o, characterLimit: 50)!);
        }

        var schedules = SchedulerPlugin.Manager.Schedules;
        if (SchedulerPlugin.Manager.CurrentSchedule != null) {
            schedules = [SchedulerPlugin.Manager.CurrentSchedule];
        }

        var listItems = schedules.Select(o => new UIPanelBuilder.ListItem<Schedule>(o.Name, o, "Saved schedules", o.Name));
        builder.AddListDetail(listItems, SchedulerPlugin.Manager.SelectedSchedule, (detail, schedule) => BuildDetail(detail, schedule, locomotive));
        builder.AddExpandingVerticalSpacer();
    }

    private static void BuildDetail(UIPanelBuilder builder, Schedule? schedule, BaseLocomotive locomotive) {
        if (SchedulerPlugin.Manager.CurrentSchedule != null) {
            schedule = SchedulerPlugin.Manager.CurrentSchedule;
        }

        if (schedule == null) {
            builder.AddLabel(SchedulerPlugin.Manager.Schedules.Any() ? "Please select a schedule." : "No schedules configured.");
            return;
        }

        if (SchedulerPlugin.Manager.CurrentSchedule == null) {
            builder.ButtonStrip(strip => {
                strip.AddButton("Execute", () => SchedulerPlugin.Manager.ExecuteSchedule(schedule, locomotive));
                strip.AddButton("Modify", () => SchedulerPlugin.Manager.ModifySchedule(schedule));
                strip.AddButton("Remove", () => SchedulerPlugin.Manager.RemoveSchedule(schedule));
            });
        } else {
            BuildAddCommandPanel(builder, locomotive, SchedulerPlugin.Manager.CurrentSchedule);
        }

        builder.VScrollView(view => {
            for (var i = 0; i < schedule.Commands.Count; i++) {
                var text = schedule.Commands[i]!.ToString();
                if (SchedulerPlugin.Manager.CurrentCommand == i) {
                    text = text.ColorYellow()!;
                }

                view.AddLabel(text);
            }
        });
    }

    private static void BuildAddCommandPanel(UIPanelBuilder builder, BaseLocomotive locomotive, Schedule schedule) {
        builder.AddField("Commands",
            builder.ButtonStrip(strip => {
                if (!SchedulerPlugin.Manager.AddCommand) {
                    strip.AddButton("Add", () => {
                        SchedulerPlugin.Manager.AddCommand = true;
                        builder.Rebuild();
                    });
                }

                strip.AddButton("Prev", () => SchedulerPlugin.Manager.PrevCommand());
                strip.AddButton("Next", () => SchedulerPlugin.Manager.NextCommand());
                if (!SchedulerPlugin.Manager.AddCommand) {
                    strip.AddButton("Remove", () => SchedulerPlugin.Manager.RemoveCommand());
                }
            })!
        );

        if (!SchedulerPlugin.Manager.AddCommand) {
            return;
        }

        builder.AddDropdown(ScheduleCommands.Commands, SchedulerPlugin.Manager.SelectedCommandIndex, o => {
            SchedulerPlugin.Manager.SelectedCommandIndex = o;
            builder.Rebuild();
        });

        var panelBuilder = ScheduleCommands.CommandPanelBuilders[SchedulerPlugin.Manager.SelectedCommandIndex]!;
        panelBuilder.Configure(locomotive);
        panelBuilder.BuildPanel(builder);

        builder.ButtonStrip(strip => { 
            strip.AddButton("Confirm", () => {
                var command = panelBuilder.CreateScheduleCommand();

                if (SchedulerPlugin.Manager.CurrentCommand != null) {
                    schedule.Commands.Insert(SchedulerPlugin.Manager.CurrentCommand.Value + 1, command);
                    ++SchedulerPlugin.Manager.CurrentCommand;
                } else {
                    schedule.Commands.Add(command);
                }

                command.Execute(locomotive);
                SchedulerPlugin.Manager.AddCommand = false;
                builder.Rebuild();
            });
            strip.AddButton("Cancel", () => {
                SchedulerPlugin.Manager.AddCommand = false;
                builder.Rebuild();
            });
        });
    }
}

