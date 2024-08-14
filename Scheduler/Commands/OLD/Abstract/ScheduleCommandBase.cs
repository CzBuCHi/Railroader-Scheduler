using System;
using Model;
using Scheduler.Data;
using UnityEngine;

namespace Scheduler.Commands.OLD.Abstract;

[Obsolete]
public abstract class ScheduleCommandBase : IScheduleCommand
{
    public abstract string Identifier { get; }

    public override string ToString()
    {
        return Identifier;
    }

    public abstract void Execute(BaseLocomotive locomotive);

    public virtual CustomYieldInstruction WaitBefore()
    {
        return new WaitForSecondsRealtime(1f);
    }

    public virtual WaitUntil? WaitUntilComplete(BaseLocomotive locomotive)
    {
        return null;
    }

    public abstract IScheduleCommand Clone();
}
