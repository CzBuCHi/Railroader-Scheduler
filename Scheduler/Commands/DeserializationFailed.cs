using System;
using System.Collections;
using System.Collections.Generic;
using Scheduler.Utility;

namespace Scheduler.Commands;

public class DeserializationFailed(string displayText) : ICommand
{
    public string DisplayText { get; } = displayText;
    public int Wage { get; } = 0;
}

public sealed class DeserializationFailedManager : CommandManager<DeserializationFailed>
{
    protected override IEnumerator ExecuteCore(Dictionary<string, object> state) {
        throw new NotSupportedException();
    }

    protected override object TryCreateCommand() {
        throw new NotSupportedException();
    }
}
