using Model;
using UnityEngine;

namespace Scheduler.Data.Commands;

public abstract class ScheduleCommandBase : IScheduleCommand
{
    public abstract string Identifier { get; }

    public override string ToString() {
        return Identifier;
    }

    public abstract void Execute(BaseLocomotive locomotive);

    public virtual CustomYieldInstruction WaitBefore() {
        return new WaitForSecondsRealtime(1f);
    }

    public virtual WaitUntil? WaitUntilComplete(BaseLocomotive locomotive) {
        return null;
    }

    public abstract IScheduleCommand Clone();
}
