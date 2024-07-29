namespace Scheduler.Data.Commands;

using HarmonyLib;
using Model;

public sealed class ScheduleCommandReleaseHandbrakes : IScheduleCommand {

    public string Identifier => "Release Handbrakes";

    public override string ToString() {
        return "Release handbrakes";
    }

    public void Execute(BaseLocomotive locomotive) {
        locomotive.EnumerateCoupled(Car.End.F)!.Do(c => c.SetHandbrake(false));
    }

    public IScheduleCommand Clone() {
        return new ScheduleCommandReleaseHandbrakes();
    }
}

public sealed class ScheduleCommandReleaseHandbrakesSerializer : ScheduleCommandSerializerBase<ScheduleCommandReleaseHandbrakes> {

}

public sealed class ScheduleCommandReleaseHandbrakesPanelBuilder : ScheduleCommandPanelBuilderBase {

    public override IScheduleCommand CreateScheduleCommand() {
        return new ScheduleCommandReleaseHandbrakes();
    }

}