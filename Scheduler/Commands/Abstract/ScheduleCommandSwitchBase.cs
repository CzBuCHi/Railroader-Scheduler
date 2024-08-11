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
        var startLocation = SchedulerUtility.FirstCarLocation(locomotive, Front ? Car.End.F : Car.End.R);

        var items = new List<(TrackSegment Segment, TrackNode Node)>();
        var index = 0;
        foreach (var item in SchedulerUtility.GetRoute(startLocation))
        {
            items.Add(item);
            if (!Graph.Shared!.IsSwitch(item.Node))
            {
                continue;
            }

            ++index;
            var distance = SchedulerUtility.Distance(startLocation, items);
            SchedulerPlugin.DebugMessage($"distance {distance}");

            var (_, lastNode) = items.Last();
            var foulingDistance = Graph.Shared.CalculateFoulingDistance(lastNode);
            SchedulerPlugin.DebugMessage($"foulingDistance {foulingDistance}");
            if (distance > foulingDistance || index > 1)
            {
                break;
            }
        }

        var (_, node) = items.Last();

        if (!SchedulerUtility.CanOperateSwitch(node, startLocation, locomotive, items))
        {
            return;
        }
        // TODO: wrong node ....
        Execute(node);
    }

    protected abstract void Execute(TrackNode node);
}
