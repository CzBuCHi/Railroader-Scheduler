using System.Collections;
using System.Collections.Generic;
using Model;
using Scheduler.Commands;
using Scheduler.Data;
using Serilog;
using UnityEngine;
using ILogger = Serilog.ILogger;

namespace Scheduler.Utility;

internal sealed class ScheduleRunner : MonoBehaviour
{
    private static readonly ILogger _Logger = Log.ForContext(typeof(ScheduleRunner))!;

    public void ExecuteSchedule(Schedule schedule, BaseLocomotive locomotive, int firstCommand = 0) {
        StartCoroutine(ExecuteCoroutine(schedule, locomotive, firstCommand));
    }

    private static IEnumerator ExecuteCoroutine(Schedule schedule, BaseLocomotive locomotive, int firstCommand) {
        _Logger.Information("Executing schedule: " + schedule.Name);

        // can be used to pass values between commands
        var state = new Dictionary<string, object> {
            ["schedule"] = schedule,
            ["locomotive"] = locomotive
        };

        for (var i = firstCommand; i < schedule.Commands.Count; i++) {
            state["index"] = i;
            var command = schedule.Commands[i]!;

            if (command is DeserializationFailed) {
                global::UI.Console.Console.shared!.AddLine($"AI Engineer [{Hyperlink.To(locomotive)}] Cannot execute command.");
                yield break;
            }

            var wait = ExecuteCommand(command, state);
            while (wait.MoveNext()) {
                yield return wait.Current;
            }
        }
    }

    private static IEnumerator ExecuteCommand(ICommand command, Dictionary<string, object> state) {
        _Logger.Information("Executing command: " + command.DisplayText);

        var locomotive = (BaseLocomotive)state["locomotive"]!;
        SchedulerPlugin.DebugMessage($"AI Engineer [{Hyperlink.To(locomotive)}] Executing {command.DisplayText}");

        var manager = ScheduleCommands.GetManager(command.GetType());
        manager.Command = command;
        return manager.Execute(state);
    }
}
