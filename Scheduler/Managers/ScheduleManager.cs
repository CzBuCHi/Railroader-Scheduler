namespace Scheduler.Managers;

using System.Collections;
using System.Collections.Generic;
using global::UI.Builder;
using Model;
using Scheduler.Data;
using UnityEngine;

public sealed class ScheduleManager : MonoBehaviour {

    public List<Schedule> Schedules => SchedulerPlugin.Settings.Schedules;
    public UIPanelBuilder Builder;

    public readonly UIState<string?> SelectedSchedule = new(null);

    public bool IsRecording;
    public string NewScheduleName = "";

    public void Start() {
        IsRecording = true;
        SchedulerPlugin.Recorder = new ScheduleRecorder();
        Builder.Rebuild();
    }

    public void Stop() {
        IsRecording = false;
        Builder.Rebuild();
    }

    public void Save() {
        var schedule = SchedulerPlugin.Recorder!.Schedule;
        schedule.Name = NewScheduleName;
        Schedules.Add(schedule);
        SchedulerPlugin.Recorder = null;

        NewScheduleName = "Schedule #" + Schedules.Count;
        Builder.Rebuild();
        SelectedSchedule.Value = NewScheduleName;
    }

    public void Discard() {
        SchedulerPlugin.Recorder = null;
        Builder.Rebuild();
    }

    public void Execute(Schedule schedule, BaseLocomotive locomotive) {
        StartCoroutine(ExecuteCoroutine(schedule, locomotive));
    }

    public void Remove(Schedule schedule) {
        Schedules.Remove(schedule);
        SelectedSchedule.Value = null;
        Builder.Rebuild();
    }

    private IEnumerator ExecuteCoroutine(Schedule schedule, BaseLocomotive locomotive) {
        foreach (var command in schedule.Commands) {
            ExecuteCommand(command, locomotive);
            yield return new WaitUntil(() => _CommandCompleted);
        }
    }

    private bool _CommandCompleted;

    private void ExecuteCommand(ScheduleCommand command, BaseLocomotive locomotive) {
        _CommandCompleted = false;

        AIWorker.ExecuteCommand(command, locomotive);
        if (command.CommandType == ScheduleCommandType.MOVE) {
            return;
        }

        _CommandCompleted = true;
    }

}