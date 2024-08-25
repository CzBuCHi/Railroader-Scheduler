using System.Collections;
using System.Collections.Generic;
using Scheduler.Utility;
using Track;

namespace Scheduler.Commands;

/// <summary> Restore state of switches, that where thrown by this schedule (See <see cref="SetSwitch"/> command). </summary>
public sealed class RestoreSwitches : ICommand
{
    public string DisplayText => "Restore switches set by this schedule";
    public int Wage { get; } = 10;
}

public sealed class RestoreSwitchesManager : CommandManager<RestoreSwitches>
{
    protected override IEnumerator ExecuteCore(Dictionary<string, object> state) {
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

            node.isThrown = pair.Value;
        }

        switches.Clear();
    }

    protected override object TryCreateCommand() {
        return new RestoreSwitches();
    }
}
