using System.Collections;
using System.Collections.Generic;
using Model;
using Scheduler.Data;
using Serilog;
using Serilog.Core;
using UnityEngine;
using ILogger = Serilog.ILogger;

namespace Scheduler.Utility;

internal sealed class ScheduleRunner : MonoBehaviour
{
    private static readonly ILogger _Logger = Log.ForContext(typeof(ScheduleRunner))!;

    public void ExecuteSchedule(Schedule schedule, BaseLocomotive locomotive) {
        StartCoroutine(ExecuteCoroutine(schedule, locomotive));
    }

    private static IEnumerator ExecuteCoroutine(Schedule schedule, BaseLocomotive locomotive) {
        _Logger.Information("Executing schedule " + schedule.Name);

        // can be used to pass values between commands
        var state = new Dictionary<string, object> {
            ["schedule"] = schedule,
            ["locomotive"] = locomotive
        };

        for (var i = 0; i < schedule.Commands.Count; i++) {
            state["index"] = i;
            var command = schedule.Commands[i]!;
            var wait = ExecuteSingle(command, state);
            while (wait.MoveNext()) {
                yield return wait.Current;
            }
        }
    }

    public static void ExecuteSingle(ICommand command, BaseLocomotive locomotive) {
        var state = new Dictionary<string, object> {
            ["locomotive"] = locomotive
        };
        ExecuteSingle(command, state);
    }
    
    private static IEnumerator ExecuteSingle(ICommand command, Dictionary<string, object> state) {
        _Logger.Information("Executing command " + command.GetType().Name);

        var locomotive = (BaseLocomotive)state["locomotive"]!;
        SchedulerPlugin.DebugMessage($"AI Engineer [{Hyperlink.To(locomotive)}] Executing {command}");

        var manager = ScheduleCommands.GetManager(command.GetType());
        manager.Command = command;
        return manager.Execute(state);
    }
}
