using System.Collections;
using System.Collections.Generic;
using Scheduler.Utility;
using Track;

namespace Scheduler.Commands;

/// <summary> Restore state of switches, that where thrown by this schedule (See <see cref="SetSwitch"/> command). </summary>
public sealed class RestoreSwitches : ICommand
{
    public string DisplayText => "Restore Switches";
}

public sealed class RestoreSwitchesManager : CommandManager<RestoreSwitches>
{
    public override IEnumerator Execute(Dictionary<string, object> state) {
        state.TryGetValue("switches", out var value);
        if (value == null) {
            yield break;
        }

        var switches = (Dictionary<string, bool>)value;
        foreach (var pair in switches) {
            var node = Graph.Shared.GetNode(pair.Key!);
            if (node == null) {
                // TODO: Log error
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
