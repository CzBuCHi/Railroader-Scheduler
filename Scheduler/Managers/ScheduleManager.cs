namespace Scheduler.Managers;

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using global::UI.Builder;
using Model;
using Scheduler.Data;
using UnityEngine;

public sealed class ScheduleManager : MonoBehaviour {

    /*

record commands:

MOVE                    * AutoEngineerOrdersHelper.SetOrdersValue(AutoEngineerMode? mode = null, bool? forward = null,int? maxSpeedMph = null, float? distance = null)
SET_SWITCH              Messenger.Default.Send<SwitchThrownDidChange>(new SwitchThrownDidChange(this));
UNCOUPLE                ? void ApplyEndGearChange(Car.LogicalEnd logicalEnd, Car.EndGearStateKey endGearStateKey, float f)
SET_HANDBRAKE           * CarPropertyChanges.SetHandbrake(this Car car, bool apply)

special commands: (game do not have buttons for those - need to add them manually in SchedulerDialog

CONNECT_AIR             * Jobs.ConnectAir
RELEASE_HANDBRAKES      * Jobs.ReleaseAllHandbrakes
RESTORE_SWITCH

 */
    public List<Schedule> Schedules => SchedulerPlugin.Settings.Schedules;
    public UIPanelBuilder Builder;
    public BaseLocomotive Locomotive = null!;

    public readonly UIState<string?> SelectedSchedule = new(null);

    public bool IsRecording;
    public Schedule? NewSchedule;
    public string NewScheduleName = "";

    public void Start() {
        IsRecording = true;
        NewSchedule = new Schedule();
        Builder.Rebuild();
    }

    public void Continue() {
        IsRecording = true;
        Builder.Rebuild();
    }

    public void Stop() {
        IsRecording = false;
        Builder.Rebuild();
    }

    public void Save() {
        NewSchedule!.Name = NewScheduleName;
        Schedules.Add(NewSchedule!);
        NewSchedule = null;

        NewScheduleName = "Schedule #" + Schedules.Count;
        Builder.Rebuild();
        SelectedSchedule.Value = NewScheduleName;
    }

    public void Discard() {
        NewSchedule = null;
        Builder.Rebuild();
    }

    public void Execute(Schedule schedule) {
        StartCoroutine(ExecuteCoroutine(schedule));
    }

    public void Remove(Schedule schedule) {
        Schedules.Remove(schedule);
        SelectedSchedule.Value = null;
        Builder.Rebuild();
    }


    private IEnumerator ExecuteCoroutine(Schedule schedule) {
        foreach (var command in schedule.Commands) {
            ExecuteCommand(command);
            yield return new WaitUntil(() => _CommandCompleted);
        }
    }

    private bool _CommandCompleted;

    private void ExecuteCommand(ScheduleCommand command) {
        _CommandCompleted = false;

        var consist = Locomotive.set.Cars.ToArray();

        if (command.CommandType == ScheduleCommandType.MOVE) {
            return;
        }

        _CommandCompleted = true;
    }

}