namespace Scheduler.Managers;

using Scheduler.Data;

public sealed class ScheduleRecorder {

    public Schedule Schedule { get; } = new();

    public void Move(bool forward, int? maxSpeed, float distance) {
        var command = new ScheduleCommand(ScheduleCommandType.MOVE, forward, maxSpeed, distance);
        // do not execute this one - code called from AutoEngineerOrdersHelper.SetOrdersValue => infinite loop
        Schedule.Commands.Add(command);
    }

    public void ConnectAir() {
        var command = new ScheduleCommand(ScheduleCommandType.CONNECT_AIR);
        AddAndExecuteCommand(command);
    }

    public void ReleaseHandbrakes() {
        var command = new ScheduleCommand(ScheduleCommandType.RELEASE_HANDBRAKES);
        AddAndExecuteCommand(command);
    }

    public void SetSwitch(bool forward, bool switchToNormal) {
        var command = new ScheduleCommand(ScheduleCommandType.SET_SWITCH, forward, switchToNormal: switchToNormal);
        AddAndExecuteCommand(command);
    }

    public void Uncouple(int index) {
        var command = new ScheduleCommand(ScheduleCommandType.UNCOUPLE, index: index);
        AddAndExecuteCommand(command);
    }

    public void SetHandbrake(int index) {
        var command = new ScheduleCommand(ScheduleCommandType.SET_HANDBRAKE, index: index);
        AddAndExecuteCommand(command);
    }

    public void RestoreSwitch(bool forward) {
        var command = new ScheduleCommand(ScheduleCommandType.RESTORE_SWITCH, forward);
        AddAndExecuteCommand(command);
    }

    private void AddAndExecuteCommand(ScheduleCommand command) {
        Schedule.Commands.Add(command);
        AIWorker.ExecuteCommand(command, TrainController.Shared!.SelectedLocomotive!);
    }

}