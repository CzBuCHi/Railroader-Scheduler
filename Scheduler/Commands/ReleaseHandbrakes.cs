﻿using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Model;
using Scheduler.Utility;

namespace Scheduler.Commands;

/// <summary> Release all handbrakes on train. </summary>
public sealed class ReleaseHandbrakes : ICommand
{
    public string DisplayText => "Release handbrakes";
}

public sealed class ReleaseHandbrakesManager : CommandManager<ReleaseHandbrakes>
{
    public override IEnumerator Execute(Dictionary<string, object> state) {
        state["wage"] = (int)state["wage"] + 1;

        var locomotive = (BaseLocomotive)state["locomotive"]!;
        locomotive.EnumerateCoupled(Car.End.F)!.Do(c => c.SetHandbrake(false));
        yield break;
    }

    protected override object TryCreateCommand() {
        return new ReleaseHandbrakes();
    }
}
