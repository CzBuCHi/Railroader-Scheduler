using System.Collections;
using System.Collections.Generic;
using Model;
using Scheduler.Utility;
using Track;

namespace Scheduler.Commands;

/// <summary> Restore state of switches, that where thrown by this schedule (See <see cref="SetSwitch"/> command). </summary>
public sealed class RestoreSwitches : ICommand
{
    public string DisplayText => "Restore switches set by this schedule";
}

public sealed class RestoreSwitchesManager : CommandManager<RestoreSwitches>
{
    public override IEnumerator Execute(Dictionary<string, object> state) {
   

        var locomotive = (BaseLocomotive)state["locomotive"]!;

        state.TryGetValue("switches", out var value);
        if (value == null) {
            yield break;
        }

        var switches = (Dictionary<string, bool>)value;
        foreach (var pair in switches) {
            var node = Graph.Shared.GetNode(pair.Key!);
            if (node == null) {
                yield break;
            }

            if (!SchedulerUtility.CanOperateSwitch(node, locomotive)) {
                yield break;
            }

            node.isThrown = pair.Value;
        }

        switches.Clear();
        state["wage"] = (int)state["wage"] + 1;
    }

    protected override object TryCreateCommand() {
        return new RestoreSwitches();
    }
}
