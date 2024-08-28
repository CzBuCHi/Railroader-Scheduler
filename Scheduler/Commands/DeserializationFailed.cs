using System;
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
        throw new NotSupportedException();
    }

    protected override object TryCreateCommand() {
        throw new NotSupportedException();
    }
}
