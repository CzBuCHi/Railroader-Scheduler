using System;
using System.Collections;
using System.Collections.Generic;
using GalaSoft.MvvmLight.Messaging;
using JetBrains.Annotations;
using Model;
using Scheduler.Data;
using UI.Builder;
using UnityEngine;

namespace Scheduler.Managers;

[UsedImplicitly]
internal sealed class ScheduleManager : MonoBehaviour
{
    public List<Schedule> Schedules => SchedulerPlugin.Settings.Schedules;
    public int SelectedCommandIndex { get; set; }

    public readonly UIState<string?> SelectedSchedule = new(null);
    public bool EditingSchedule;
    public bool EditingExistingSchedule => _EditedSchedule != null;
    public bool AddCommand;

    private Schedule? _EditedSchedule;
    public Schedule? CurrentSchedule;

    private string _ScheduleName = "";

    public string ScheduleName {
        get => _ScheduleName;
        set {
            if (_ScheduleName == value) {
                return;
            }

            _ScheduleName = value;
            ScheduleNameConflict = Schedules.FindIndex(o => o != _EditedSchedule && o.Name == _ScheduleName) != -1;
        }
    }

    private bool _ScheduleNameConflict;

    public bool ScheduleNameConflict {
        get => _ScheduleNameConflict;
        private set {
            if (_ScheduleNameConflict == value) {
                return;
            }

            _ScheduleNameConflict = value;
            RebuildDialog();
        }
    }

    public int? CurrentCommand;

    public void Cleanup() {
        EditingSchedule = false;
        AddCommand = false;
        CurrentSchedule = null;
        ScheduleName = "Schedule #" + Schedules.Count;
        CurrentCommand = null;
        _EditedSchedule = null;
        SelectedSchedule.Value = null;
    }

    public void CreateNewSchedule() {
        EditingSchedule = true;
        CurrentSchedule = new Schedule();
        RebuildDialog();
    }

    public void SaveSchedule() {
        EditingSchedule = false;

        if (EditingExistingSchedule) {
            var index = Schedules.FindIndex(o => o.Name == SelectedSchedule.Value);
            Schedules.RemoveAt(index);
            Schedules.Insert(index, CurrentSchedule!);
        } else {
            if (ScheduleNameConflict) {
                return;
            }

            CurrentSchedule!.Name = ScheduleName;
            Schedules.Add(CurrentSchedule);
        }

        ScheduleName = "Schedule #" + Schedules.Count;
        _EditedSchedule = null;
        CurrentSchedule = null;
        CurrentCommand = null;
        SchedulerPlugin.SaveSettings();
        RebuildDialog();
        RebuildCarInspector();
    }

    public void DiscardSchedule() {
        EditingSchedule = false;
        AddCommand = false;
        _EditedSchedule = null;
        CurrentSchedule = null;
        RebuildDialog();
    }

    public void ExecuteSchedule(Schedule schedule, BaseLocomotive locomotive) {
        StartCoroutine(ExecuteCoroutine(schedule, locomotive));
    }

    public void ModifySchedule(Schedule schedule) {
        EditingSchedule = true;
        _EditedSchedule = schedule;
        CurrentSchedule = schedule.Clone();
        CurrentCommand = 0;
        ScheduleName = CurrentSchedule.Name;
        RebuildDialog();
    }

    public void RemoveSchedule(Schedule schedule) {
        Schedules.Remove(schedule);
        SelectedSchedule.Value = null;
        SchedulerPlugin.SaveSettings();
        RebuildDialog();
    }

    public void PrevCommand() {
        CurrentCommand = Math.Max(0, CurrentCommand!.Value - 1);
        RebuildDialog();
    }

    public void RemoveCommand() {
        CurrentSchedule!.Commands.RemoveAt(CurrentCommand!.Value);
        CurrentCommand = Math.Max(0, CurrentCommand!.Value - 1);
        RebuildDialog();
    }

    public void NextCommand() {
        CurrentCommand = Math.Min(CurrentSchedule!.Commands.Count - 1, CurrentCommand!.Value + 1);
        RebuildDialog();
    }

    private static void RebuildDialog() {
        Messenger.Default!.Send(ScheduleRebuildMessage.Instance);
    }

    private static void RebuildCarInspector() {
        Messenger.Default!.Send(CarInspectorRebuildMessage.Instance);
    }

    private static IEnumerator ExecuteCoroutine(Schedule schedule, BaseLocomotive locomotive) {
        foreach (var command in schedule.Commands) {
            SchedulerPlugin.DebugMessage($"AI Engineer [{Hyperlink.To(locomotive)}] Executing {command}");

            try {
                command.Execute(locomotive);
            } catch (Exception e) {
                global::UI.Console.Console.shared!.AddLine($"AI Engineer [{Hyperlink.To(locomotive)}]: Pee in the cup moment: " + e.Message);
                throw;
            }

            yield return command.WaitBefore();
            yield return command.WaitUntilComplete(locomotive);
        }
    }
}

internal sealed class ScheduleRebuildMessage
{
    public static readonly ScheduleRebuildMessage Instance = new();
}

internal sealed class CarInspectorRebuildMessage
{
    public static readonly CarInspectorRebuildMessage Instance = new();
}
