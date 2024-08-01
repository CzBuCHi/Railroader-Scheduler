using HarmonyLib;
using Model;

namespace Scheduler.Data.Commands;

public sealed class ScheduleCommandReleaseHandbrakes : ScheduleCommandBase
{
    public override string Identifier => "Release Handbrakes";

    public override void Execute(BaseLocomotive locomotive) {
        locomotive.EnumerateCoupled(Car.End.F)!.Do(c => c.SetHandbrake(false));
    }

    public override IScheduleCommand Clone() {
        return new ScheduleCommandReleaseHandbrakes();
    }
}

public sealed class ScheduleCommandReleaseHandbrakesSerializer : ScheduleCommandSerializerBase<ScheduleCommandReleaseHandbrakes>
{
}

public sealed class ScheduleCommandReleaseHandbrakesPanelBuilder : ScheduleCommandPanelBuilderBase
{
    public override IScheduleCommand CreateScheduleCommand() {
        return new ScheduleCommandReleaseHandbrakes();
    }
}
