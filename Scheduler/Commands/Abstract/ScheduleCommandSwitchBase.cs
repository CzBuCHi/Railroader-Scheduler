using System.Collections.Generic;
using System.Linq;
using Model;
using Track;

namespace Scheduler.Commands.Abstract;

public abstract class ScheduleCommandSwitchBase(bool front) : ScheduleCommandBase
{
    public bool Front { get; } = front;

    public override void Execute(BaseLocomotive locomotive)
    {
        SchedulerUtility.GetDistanceForSwitchOrder(1, false, false, locomotive, Front, out var targetSwitch);
        if (targetSwitch == null || !SchedulerUtility.CanOperateSwitch(targetSwitch, locomotive)) {
            return;
        }
        
        Execute(targetSwitch);
    }

    protected abstract void Execute(TrackNode node);
}
