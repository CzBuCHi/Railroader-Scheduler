using System;
using System.Collections;
using System.Collections.Generic;
using Model;
using Scheduler.Data;
using UI.Builder;
using UnityEngine;

namespace Scheduler.Managers;

internal sealed class ScheduleManager : MonoBehaviour
{
    public readonly UIState<string?> SelectedSchedule = new(null);
    public UIPanelBuilder Builder;

    public bool IsRecording;
    public string NewScheduleName = "";
    public int? CurrentCommand;

    public bool AddCommand;
    public int SelectedCommandIndex;

    public List<Schedule> Schedules => SchedulerPlugin.Settings.Schedules;

    public void StartRecording() {
        IsRecording = true;
        SchedulerPlugin.NewSchedule = new Schedule();
        Builder.Rebuild();
    }

    public void StopRecording() {
        IsRecording = false;
        CurrentCommand = null;
        Builder.Rebuild();
    }

    public void ModifySchedule(Schedule schedule) {
        IsRecording = true;
        SchedulerPlugin.NewSchedule = schedule.Clone();
        CurrentCommand = 0;
        Builder.Rebuild();
    }

    public void Save() {
        var schedule = SchedulerPlugin.NewSchedule!;
        if (SelectedSchedule.Value != null) {
            var index = Schedules.FindIndex(o => o.Name == schedule.Name);
            Schedules.RemoveAt(index);
            Schedules.Insert(index, schedule);
        } else {
            schedule.Name = NewScheduleName;
            Schedules.Add(schedule);
        }

        SchedulerPlugin.NewSchedule = null;

        NewScheduleName = "Schedule #" + Schedules.Count;
        Builder.Rebuild();
        SelectedSchedule.Value = NewScheduleName;
        SchedulerPlugin.SaveSettings();
    }

    public void Discard() {
        SchedulerPlugin.NewSchedule = null;
        Builder.Rebuild();
    }

    public void ExecuteSchedule(Schedule schedule, BaseLocomotive locomotive) {
        StartCoroutine(ExecuteCoroutine(schedule, locomotive));
    }

    public void RemoveSchedule(Schedule schedule) {
        Schedules.Remove(schedule);
        SelectedSchedule.Value = null;
        Builder.Rebuild();
        SchedulerPlugin.SaveSettings();
    }

    private IEnumerator ExecuteCoroutine(Schedule schedule, BaseLocomotive locomotive) {
        foreach (var command in schedule.Commands) {
            ExecuteCommand(command, locomotive);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitUntil(() => {
                SchedulerPlugin.DebugMessage($"AI Engineer [{Hyperlink.To(locomotive)}] Still moving ...");

                return locomotive.IsStopped(0.5f);
            });
            SchedulerPlugin.DebugMessage($"AI Engineer [{Hyperlink.To(locomotive)}] Stopped ");
        }
    }

    private void ExecuteCommand(IScheduleCommand command, BaseLocomotive locomotive) {
        SchedulerPlugin.DebugMessage($"AI Engineer [{Hyperlink.To(locomotive)}] Executing {command}");

        try {
            command.Execute(locomotive);
        }
        catch (Exception e) {
            global::UI.Console.Console.shared!.AddLine(
                $"AI Engineer [{Hyperlink.To(locomotive)}]: Pee in the cup moment: " + e.Message);
            throw;
        }
    }

    public void PrevCommand() {
        CurrentCommand = Math.Max(0, CurrentCommand!.Value - 1);
        Builder.Rebuild();
    }

    public void NextCommand() {
        CurrentCommand = Math.Min(SchedulerPlugin.NewSchedule!.Commands.Count - 1, CurrentCommand!.Value + 1);
        Builder.Rebuild();
    }

    public void RemoveCommand() {
        SchedulerPlugin.NewSchedule!.Commands.RemoveAt(CurrentCommand!.Value);
        PrevCommand();
    }
}