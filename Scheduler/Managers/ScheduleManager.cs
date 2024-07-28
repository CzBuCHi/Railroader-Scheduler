namespace Scheduler.Managers;

using System;
using System.Collections;
using System.Collections.Generic;
using global::UI.Builder;
using KeyValue.Runtime;
using Model;
using Model.AI;
using Newtonsoft.Json.Linq;
using Scheduler.Data;
using Scheduler.Data.Commands;
using UnityEngine;

internal sealed class ScheduleManager : MonoBehaviour {

    public List<Schedule> Schedules => SchedulerPlugin.Settings.Schedules;
    public UIPanelBuilder Builder;

    public readonly UIState<string?> SelectedSchedule = new(null);

    public bool IsRecording;
    public string NewScheduleName = "";

    public void StartRecording() {
        IsRecording = true;
        SchedulerPlugin.NewSchedule = new Schedule();
        Builder.Rebuild();
    }

    public void StopRecording() {
        IsRecording = false;
        Builder.Rebuild();
    }

    public void Save() {
        var schedule = SchedulerPlugin.NewSchedule!;
        schedule.Name = NewScheduleName;
        Schedules.Add(schedule);
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

    public void Execute(Schedule schedule, BaseLocomotive locomotive) {
        StartCoroutine(ExecuteCoroutine(schedule, locomotive));
    }

    public void Remove(Schedule schedule) {
        Schedules.Remove(schedule);
        SelectedSchedule.Value = null;
        Builder.Rebuild();
        SchedulerPlugin.SaveSettings();
    }

    private IEnumerator ExecuteCoroutine(Schedule schedule, BaseLocomotive locomotive) {
        foreach (var command in schedule.Commands) {
            ExecuteCommand(command, locomotive);
            yield return new WaitUntil(() => _CommandCompleted);
        }
    }

    private bool _CommandCompleted;

    private static readonly Serilog.ILogger _Logger = Serilog.Log.ForContext(typeof(ScheduleManager))!;

    private void ExecuteCommand(IScheduleCommand command, BaseLocomotive locomotive) {
        _CommandCompleted = false;

        _Logger.Information($"AIWorker [{locomotive}] Executing {command}");
        SchedulerPlugin.DebugMessage($"Executing {command}");

        try {
            command.Execute(locomotive);
            if (command is ScheduleCommandMove) {
                DisposableWrap disposable = new DisposableWrap();
                var persistence = new AutoEngineerPersistence(locomotive.KeyValueObject!);
                disposable.Disposable = persistence.ObserveOrders(orders => {
                    SchedulerPlugin.DebugMessage("Status: " + orders);
                    if (!orders.Enabled) {
                        _CommandCompleted = true;
                        disposable.Dispose();
                    }
                });
                return;
            }

        } catch (Exception e) {
            global::UI.Console.Console.shared!.AddLine("[AI]" + e.Message);
            throw;
        }

        _CommandCompleted = true;
    }

    private class DisposableWrap : IDisposable {

        public IDisposable? Disposable { get; set; }

        public void Dispose() {
            Disposable?.Dispose();
        }

    }

}