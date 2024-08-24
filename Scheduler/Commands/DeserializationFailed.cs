using System.Collections;
using System.Collections.Generic;
using Scheduler.Utility;

namespace Scheduler.Commands;

public class DeserializationFailed(string displayText) : ICommand
{
    public string DisplayText { get; } = displayText;
}

public sealed class DeserializationFailedManager : CommandManager<DeserializationFailed>
{
    public override IEnumerator Execute(Dictionary<string, object> state) {
        yield break;
    }

    protected override object TryCreateCommand() {
        return new DeserializationFailed("DeserializationFailed");
    }
}
