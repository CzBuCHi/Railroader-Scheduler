using System;
using System.Linq;
using GalaSoft.MvvmLight.Messaging;
using Model;
using Scheduler.Data;
using Scheduler.Messages;
using UI.Builder;

namespace Scheduler.UI;

public sealed class SchedulerDialog : SchedulerDialogBase
{
    public static SchedulerDialog Shared {
        get => Instance;
        set => Instance = value;
    }

    private readonly SchedulePanel _SchedulePanel = new();
    private readonly UIState<string?> _SelectedScheduleName = new(null);

    private UiState _State = UiState.Initial;

    private string? _ScheduleName;
    private string _NewScheduleName = "";
    private bool _NewScheduleNameConflict;

    protected override void BuildWindow(UIPanelBuilder builder, BaseLocomotive locomotive) {
        builder.RebuildOnEvent<RebuildScheduleDialog>();
        builder.AddTitle("Scheduler", "Locomotive: " + locomotive.DisplayName);

        switch (_State) {
            case UiState.Initial:
                builder.ButtonStrip(strip => {
                    var hasNoScheduleSelected = _SelectedScheduleName.Value == null;
                    var selectedScheduleNotValid = SchedulerPlugin.Settings.Schedules.FirstOrDefault(o => o.Name == _SelectedScheduleName.Value)?.IsValid != true;

                    strip.AddButton("Create", CreateSchedule);
                    strip.AddButton("Remove", SetUiState(UiState.Remove)).Disable(hasNoScheduleSelected);
                    strip.AddButton("Rename", RenameSchedule).Disable(hasNoScheduleSelected);
                    strip.AddButton("Modify", SetUiState(UiState.Modify)).Disable(hasNoScheduleSelected);
                    strip.AddButton("Execute", Execute(locomotive)).Disable(hasNoScheduleSelected || selectedScheduleNotValid);
                });

                var listItems = SchedulerPlugin.Settings.Schedules.Select(o => new UIPanelBuilder.ListItem<Schedule>(o.Name, o, "Saved schedules", o.Name));
                builder.AddListDetail(listItems, _SelectedScheduleName, BuildDetail);
                break;

            case UiState.Create:
                builder.AddSection("Create new schedule", section => { 
                    section.AddField("Schedule Name", builder.AddInputField(_NewScheduleName, UpdateNewScheduleName, characterLimit: 50)!);
                    if (_NewScheduleNameConflict) {
                        section.AddField("Error".ColorRed(), "Given name is already used by another schedule.");
                    }
                    BuildBottomButtonStrip(section);
                });
                break;

            case UiState.Rename:
                builder.AddSection($"Rename schedule '{_ScheduleName}'", section => { 
                    section.AddField("Schedule Name", builder.AddInputField(_NewScheduleName, UpdateNewScheduleName, characterLimit: 50)!);
                    if (_NewScheduleNameConflict) {
                        section.AddField("Error".ColorRed(), "Given name is already used by another schedule.");
                    }
                    BuildBottomButtonStrip(section);
                });
                break;

            case UiState.Remove:
                builder.AddSection($"Remove schedule '{_ScheduleName}'", BuildBottomButtonStrip);
                break;

            case UiState.Modify:
                builder.AddSection($"Modify schedule '{_ScheduleName}'", section => { 
                    _SchedulePanel.BuildPanel(section, _ScheduleName!, locomotive);
                    BuildBottomButtonStrip(section);
                });
                break;
        }
    }

    private void BuildBottomButtonStrip(UIPanelBuilder builder) {
        builder.ButtonStrip(strip => {
            switch (_State) {
                case UiState.Create:
                    strip.AddButton("Create", ConfirmCreateSchedule).Disable(_NewScheduleNameConflict);
                    break;

                case UiState.Remove:
                    strip.AddButton("Confirm", RemoveSchedule);
                    break;

                case UiState.Rename:
                    strip.AddButton("Confirm", ConfirmRenameSchedule).Disable(_NewScheduleNameConflict);
                    break;

                case UiState.Modify:
                    strip.AddButton("Save", ConfirmModifySchedule);
                    break;
            }

            strip.AddButton("Cancel", SetUiState(UiState.Initial));
        });
        builder.AddExpandingVerticalSpacer();
    }

    private void BuildDetail(UIPanelBuilder builder, Schedule? schedule) {
        if (schedule == null) {
            builder.AddLabel(SchedulerPlugin.Settings.Schedules.Any() ? "Please select a schedule." : "No schedules configured.");
            return;
        }

        if (_ScheduleName != schedule.Name) {
            _ScheduleName = schedule.Name;
            RebuildScheduleDialog();
            return;
        }

        builder.VScrollView(view => {
            for (var i = 0; i < schedule.Commands.Count; i++) {
                var text = schedule.Commands[i]!.DisplayText;
                view.AddLabel(text);
            }
        });
    }

    private void CreateSchedule() {
        _State = UiState.Create;
        UpdateNewScheduleName($"New schedule #{SchedulerPlugin.Settings.Schedules.Count + 1}");
    }

    private void RenameSchedule() {
        _NewScheduleName = _ScheduleName!;
        _State = UiState.Rename;
        RebuildScheduleDialog();
    }

    private void ConfirmCreateSchedule() {
        var schedule = new Schedule { Name = _NewScheduleName };
        SchedulerPlugin.Settings.Schedules.Add(schedule);
        _SelectedScheduleName.Value = _NewScheduleName;
        _State = UiState.Initial;
        RebuildScheduleDialog();
    }

    private void ConfirmRenameSchedule() {
        var index = SchedulerPlugin.Settings.Schedules.FindIndex(o => o.Name == _SelectedScheduleName.Value);
        SchedulerPlugin.Settings.Schedules[index]!.Name = _NewScheduleName;
        SchedulerPlugin.SaveSettings();
        _SelectedScheduleName.Value = _NewScheduleName;
        _State = UiState.Initial;
        RebuildScheduleDialog();
    }

    private void UpdateNewScheduleName(string newScheduleName) {
        _NewScheduleName = newScheduleName;
        _NewScheduleNameConflict = _NewScheduleName != _ScheduleName && SchedulerPlugin.Settings.Schedules.Any(o => o.Name == newScheduleName);
        RebuildScheduleDialog();
    }

    private Action SetUiState(UiState state) {
        return () => {
            _State = state;
            RebuildScheduleDialog();
        };
    }

    private void RemoveSchedule() { 
        var index = SchedulerPlugin.Settings.Schedules.FindIndex(o => o.Name == _SelectedScheduleName.Value);
        SchedulerPlugin.Settings.Schedules.RemoveAt(index);
        SchedulerPlugin.SaveSettings();
        _SelectedScheduleName.Value = null;
        _State = UiState.Initial;
        RebuildScheduleDialog();
    }

    private enum UiState
    {
        Initial,
        Create,
        Remove,
        Rename,
        Modify
    }

    private static void RebuildScheduleDialog() {
        Messenger.Default.Send(new RebuildScheduleDialog());
    }

    private void ConfirmModifySchedule() {
        var index = SchedulerPlugin.Settings.Schedules.FindIndex(o => o.Name == _SelectedScheduleName.Value);
        SchedulerPlugin.Settings.Schedules[index] = _SchedulePanel.Schedule;
        SchedulerPlugin.SaveSettings();
        _State = UiState.Initial;
        RebuildScheduleDialog();
    }

    private Action Execute(BaseLocomotive locomotive) {
        return () => {
            var index = SchedulerPlugin.Settings.Schedules.FindIndex(o => o.Name == _SelectedScheduleName.Value);
            var schedule = SchedulerPlugin.Settings.Schedules[index]!;
            SchedulerPlugin.Runner.ExecuteSchedule(schedule, locomotive);
        };
    }
}
