using System;
using System.Collections.Generic;
using System.Linq;
using GalaSoft.MvvmLight.Messaging;
using Model;
using Newtonsoft.Json;
using Scheduler.Data;
using Scheduler.Messages;
using Scheduler.Utility;
using Serilog;
using UI.Builder;
using UI.Common;

namespace Scheduler.UI;

public sealed class SchedulerDialog
{
    private static SchedulerDialog? _Shared;

    public static SchedulerDialog Shared {
        get => _Shared ??= new SchedulerDialog();
        set {
            if (_Shared != null) {
                _Shared._Window.OnShownDidChange -= _Shared.WindowOnOnShownDidChange;
            }

            _Shared = value;
        }
    }

    private static readonly ILogger _Logger = Log.ForContext(typeof(SchedulerDialog))!;

    private readonly Window _Window = SchedulerPlugin.UiHelper.CreateWindow(800, 500, Window.Position.Center);

    private SchedulerDialog() {
        _Window.Title = "AI Scheduler";
        _Window.OnShownDidChange += WindowOnOnShownDidChange;
        _State = new Initial();
    }

    private void WindowOnOnShownDidChange(bool isShown) {
        if (!isShown) {
            Messenger.Default!.Send(new RebuildCarInspectorAIPanel());
        }
    }

    private BaseLocomotive _Locomotive = null!;

    public void ShowWindow(BaseLocomotive locomotive) {
        _Locomotive = locomotive;
        _State = new Initial();
        _Logger.Information("BuildWindow: " + JsonConvert.SerializeObject(_State));

        SchedulerPlugin.UiHelper.PopulateWindow(_Window, BuildWindow);
        if (!_Window.IsShown) {
            _Window.ShowWindow();
        }
    }

    private IState _State;

    private void SetState(IState state) {
        _State = state;
        _Logger.Information("SetState: " + state.GetType().Name + JsonConvert.SerializeObject(state));

        SchedulerPlugin.ShowTrackSwitchVisualizers = _State is ModifySchedule { EditMode:  not EditMode.None } modifySchedule && ScheduleCommands.GetManager(modifySchedule.CommandTypeIndex).ShowTrackSwitchVisualizers;
        Messenger.Default!.Send(new RebuildSchedulePanel());
    }

    private List<Schedule> Schedules => SchedulerPlugin.Settings.Schedules;

    private void BuildWindow(UIPanelBuilder builder) {
        builder.RebuildOnEvent<RebuildSchedulePanel>();

        _Logger.Information("BuildWindow: " + JsonConvert.SerializeObject(_State));

        builder.ButtonStrip(strip => {
            if (_State is Initial initial) {
                var hasNoScheduleSelected = initial.Schedule == null;

                strip.AddButton("Create new", () => SetState(new NewSchedule($"New schedule #{Schedules.Count + 1}", false)));
                strip.AddButton("Remove", () => SetState(new RemoveSchedule(initial.Name.Value!))).Disable(hasNoScheduleSelected);
                strip.AddButton("Rename", () => SetState(new RenameSchedule(initial.Name.Value!, initial.Name.Value!, false))).Disable(hasNoScheduleSelected);
                strip.AddButton("Modify", ModifyScheduleBegin).Disable(hasNoScheduleSelected);
                strip.AddButton("Execute", () => ExecuteSchedule(initial.Schedule!, 0)).Disable(hasNoScheduleSelected);
            }

            switch (_State) {
                case NewSchedule:
                    strip.AddButton("Save", NewScheduleEnd);
                    strip.AddButton("Cancel", () => SetState(new Initial()));
                    break;

                case RemoveSchedule:
                    strip.AddButton("Confirm", RemoveScheduleEnd);
                    strip.AddButton("Cancel", () => SetState(new Initial()));
                    break;

                case RenameSchedule:
                    strip.AddButton("Save", RenameScheduleEnd);
                    strip.AddButton("Cancel", () => SetState(new Initial()));
                    break;

                case ModifySchedule modifySchedule:
                    strip.AddButton("Save", ModifyScheduleEnd);
                    strip.AddButton("Cancel", () => SetState(new Initial()));
                    strip.AddButton("Execute from current", () => ExecuteSchedule(modifySchedule.Schedule, modifySchedule.CommandIndex));
                    break;
            }
        });

        if (_State is NewSchedule newSchedule) {
            var labelText = "Schedule Name";
            if (newSchedule.Conflict) {
                labelText = labelText.ColorRed()!;
            }

            builder.AddField(labelText, builder.AddInputField(newSchedule.Name, newName => { SetState(new NewSchedule(newName, Schedules.FindIndex(o => o.Name == newName) != -1)); }, characterLimit: 50)!);
        }

        if (_State is RenameSchedule renameSchedule) {
            var labelText = "Schedule Name";
            if (renameSchedule.Conflict) {
                labelText = labelText.ColorRed()!;
            }

            builder.AddField(labelText, builder.AddInputField(renameSchedule.Name, newName => { SetState(renameSchedule with { Name = newName, Conflict = Schedules.FindIndex(o => o.Name == newName) != -1 }); }, characterLimit: 50)!);
        }

        switch (_State) {
            case ModifySchedule modifySchedule:
                _Window.Title = $"AI Scheduler | {modifySchedule.Schedule.Name}";
                BuildEditor(builder);
                break;

            case Initial initial: {
                _Window.Title = "AI Scheduler";
                var listItems = Schedules.Select(o => new UIPanelBuilder.ListItem<Schedule>(o.Name, o, "Saved schedules", o.Name));
                builder.AddListDetail(listItems, initial.Name, BuildDetail);
                break;
            }
        }

        builder.AddExpandingVerticalSpacer();
    }

    private void BuildDetail(UIPanelBuilder builder, Schedule? schedule) {
        if (schedule == null) {
            builder.AddLabel(Schedules.Any() ? "Please select a schedule." : "No schedules configured.");
            return;
        }

        var initial = (Initial)_State;
        if (initial.Schedule != schedule) {
            SetState(initial with { Schedule = schedule });
            return;
        }

        BuildCommandList(builder, schedule);
    }

    private void BuildEditor(UIPanelBuilder builder) {
        var modifySchedule = (ModifySchedule)_State;

        builder.AddField("Commands",
            builder.ButtonStrip(strip => {
                var hasCommands = modifySchedule.Schedule.Commands.Count > 0;
                var isFirst = (hasCommands && modifySchedule.CommandIndex == 0);
                var isLast = (hasCommands && modifySchedule.CommandIndex == modifySchedule.Schedule.Commands.Count - 1);

                SchedulerPlugin.DebugMessage(
                    $"count: {modifySchedule.Schedule.Commands.Count}, " +
                    $"commandIndex: {modifySchedule.CommandIndex}, " +
                    $"notFirst: {isFirst}, " +
                    $"notLast: {isLast}"
                );

                strip.AddButton("Add", AddCommandBegin);
                strip.AddButton("Remove", RemoveCommand).Disable(modifySchedule.Schedule.Commands.Count == 0);
                strip.AddButton("Modify", ModifyCommandBegin).Disable(modifySchedule.Schedule.Commands.Count == 0);
                strip.AddButton("<<", FirstCommand).Disable(isFirst);
                strip.AddButton("<", PrevCommand).Disable(isFirst);
                strip.AddButton(">", NextCommand).Disable(isLast);
                strip.AddButton(">>", LastCommand).Disable(isLast);
                strip.AddButton("Move up", MoveUpCommand).Disable(isFirst);
                strip.AddButton("Move down", MoveDownCommand).Disable(isLast);
            })!
        );

        if (modifySchedule.EditMode == EditMode.None) {
            BuildCommandList(builder, modifySchedule.Schedule);
            return;
        }

        builder.AddField("Command",
            builder.AddDropdown(ScheduleCommands.Commands, modifySchedule.CommandTypeIndex, o => SetState(modifySchedule with { CommandTypeIndex = o }))!
        );

        var manager = ScheduleCommands.GetManager(modifySchedule.CommandTypeIndex);
        manager.BuildPanel(builder, _Locomotive);

        builder.ButtonStrip(strip => {
            strip.AddButton("Confirm", modifySchedule.EditMode == EditMode.New ? AddCommandEnd : ModifyCommandEnd);
            strip.AddButton("Cancel", () => SetState(modifySchedule with { EditMode = EditMode.None }));
        });
    }

    private void BuildCommandList(UIPanelBuilder builder, Schedule schedule) {
        var modifySchedule = _State as ModifySchedule;

        builder.VScrollView(view => {
            for (var i = 0; i < schedule.Commands.Count; i++) {
                var text = schedule.Commands[i]!.DisplayText;
                if (modifySchedule != null && modifySchedule.CommandIndex == i) {
                    text = text.ColorYellow()!;
                }

                view.AddLabel(text);
            }
        });
    }

    private void NewScheduleEnd() {
        var name = ((NewSchedule)_State).Name;
        Schedules.Add(new Schedule { Name = name });
        SchedulerPlugin.SaveSettings();
        SetState(new Initial { Name = { Value = name } });
    }

    private void RemoveScheduleEnd() {
        var name = ((RemoveSchedule)_State).Name;

        var index = Schedules.FindIndex(o => o.Name == name);
        Schedules.RemoveAt(index);
        SchedulerPlugin.SaveSettings();
        if (index > 0) {
            SetState(new Initial { Name = { Value = Schedules[index - 1]!.Name } });
            return;
        }

        SetState(new Initial());
    }

    private void RenameScheduleEnd() {
        var renameSchedule = (RenameSchedule)_State;
        var index = Schedules.FindIndex(o => o.Name == renameSchedule.OldName);
        Schedules[index]!.Name = renameSchedule.Name;
        SchedulerPlugin.SaveSettings();
        SetState(new Initial { Name = { Value = renameSchedule.Name } });
    }

    private void ModifyScheduleBegin() {
        var initial = (Initial)_State;
        var schedule = Schedules.First(o => o.Name == initial.Name.Value);
        SetState(new ModifySchedule(schedule, 0, 0, EditMode.None));
    }

    private void ModifyScheduleEnd() {
        var modifySchedule = (ModifySchedule)_State;
        var index = Schedules.FindIndex(o => o.Name == modifySchedule.Schedule.Name);
        Schedules.RemoveAt(index);
        Schedules.Insert(index, modifySchedule.Schedule);
        SchedulerPlugin.SaveSettings();
        SetState(new Initial { Name = { Value = modifySchedule.Schedule.Name } });
    }

    private void ExecuteSchedule(Schedule schedule, int firstCommand) {
        SchedulerPlugin.Runner.ExecuteSchedule(schedule, _Locomotive, firstCommand);
    }

    private void AddCommandBegin() {
        SetState((ModifySchedule)_State with { EditMode = EditMode.New });
    }

    private void AddCommandEnd() {
        var modifySchedule = (ModifySchedule)_State;
        var manager = ScheduleCommands.GetManager(modifySchedule.CommandTypeIndex);

        var command = manager.CreateCommand();

        if (modifySchedule.Schedule.Commands.Count > modifySchedule.CommandIndex) {
            modifySchedule.Schedule.Commands.Insert(modifySchedule.CommandIndex + 1, command);
        } else {
            modifySchedule.Schedule.Commands.Add(command);
        }

        SchedulerPlugin.SelectedSwitch = null;
        SchedulerPlugin.ShowTrackSwitchVisualizers = false;
        SetState(modifySchedule with { CommandIndex = modifySchedule.CommandIndex + 1, EditMode = EditMode.None });
    }

    private void RemoveCommand() {
        var modifySchedule = (ModifySchedule)_State;
        modifySchedule.Schedule.Commands.RemoveAt(modifySchedule.CommandIndex);
        SetState(modifySchedule with { CommandIndex = Math.Max(0, modifySchedule.CommandIndex - 1) });
    }

    private void ModifyCommandBegin() {
        SetState((ModifySchedule)_State with { EditMode = EditMode.Edit });
    }

    private void ModifyCommandEnd() {
        var modifySchedule = (ModifySchedule)_State;
        var manager = ScheduleCommands.GetManager(modifySchedule.CommandTypeIndex);
        var command = manager.CreateCommand();
        modifySchedule.Schedule.Commands[modifySchedule.CommandIndex] = command;
        SchedulerPlugin.SelectedSwitch = null;
        SchedulerPlugin.ShowTrackSwitchVisualizers = false;
        SetState(modifySchedule with { CommandIndex = modifySchedule.CommandIndex + 1, EditMode = EditMode.None });
    }
    

    private void FirstCommand() {
        var modifySchedule = (ModifySchedule)_State;
        SetState(modifySchedule with { CommandIndex = 0 });
    }

    private void PrevCommand() {
        var modifySchedule = (ModifySchedule)_State;
        SetState(modifySchedule with { CommandIndex = Math.Max(0, modifySchedule.CommandIndex - 1) });
    }

    private void NextCommand() {
        var modifySchedule = (ModifySchedule)_State;
        SetState(modifySchedule with { CommandIndex = Math.Min(modifySchedule.Schedule.Commands.Count - 1, modifySchedule.CommandIndex + 1) });
    }

    private void LastCommand() {
        var modifySchedule = (ModifySchedule)_State;
        SetState(modifySchedule with { CommandIndex = modifySchedule.Schedule.Commands.Count - 1 });
    }

    private void MoveUpCommand() {
        var modifySchedule = (ModifySchedule)_State;

        (modifySchedule.Schedule.Commands[modifySchedule.CommandIndex], modifySchedule.Schedule.Commands[modifySchedule.CommandIndex - 1]) =
            (modifySchedule.Schedule.Commands[modifySchedule.CommandIndex - 1], modifySchedule.Schedule.Commands[modifySchedule.CommandIndex]);

        SetState(modifySchedule with { CommandIndex = modifySchedule.CommandIndex - 1 });
    }

    private void MoveDownCommand() {
        var modifySchedule = (ModifySchedule)_State;

        (modifySchedule.Schedule.Commands[modifySchedule.CommandIndex], modifySchedule.Schedule.Commands[modifySchedule.CommandIndex + 1]) =
            (modifySchedule.Schedule.Commands[modifySchedule.CommandIndex + 1], modifySchedule.Schedule.Commands[modifySchedule.CommandIndex]);

        SetState(modifySchedule with { CommandIndex = modifySchedule.CommandIndex + 1 });
    }
}

public interface IState;

public sealed record Initial(Schedule? Schedule = null) : IState
{
    public readonly UIState<string?> Name = new(null);
}

public sealed record NewSchedule(string Name, bool Conflict) : IState;

public sealed record RemoveSchedule(string Name) : IState;

public sealed record RenameSchedule(string OldName, string Name, bool Conflict) : IState;

public sealed record ModifySchedule(Schedule Schedule, int CommandTypeIndex, int CommandIndex, EditMode EditMode) : IState;

public enum EditMode { None, New, Edit }
