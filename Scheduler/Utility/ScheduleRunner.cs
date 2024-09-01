using System.Collections;
using System.Collections.Generic;
using GalaSoft.MvvmLight.Messaging;
using Game.Notices;
using Game.State;
using Model;
using Scheduler.Data;
using Scheduler.Messages;
using Serilog;
using UnityEngine;
using ILogger = Serilog.ILogger;

namespace Scheduler.Utility;

internal sealed class ScheduleRunner : MonoBehaviour
{
    private static readonly ILogger _Logger = Log.ForContext(typeof(ScheduleRunner))!;

    public void ExecuteSchedule(Schedule schedule, BaseLocomotive locomotive, int firstCommand = 0) {
        if (!schedule.IsValid) {
            global::UI.Console.Console.shared!.AddLine($"AI Engineer [{Hyperlink.To(locomotive)}] Schedule has invalid commands.");
            return;
        }

        StartCoroutine(ExecuteCoroutine(schedule, locomotive, firstCommand));
    }

    private static IEnumerator ExecuteCoroutine(Schedule schedule, BaseLocomotive locomotive, int firstCommand) {
        _Logger.Information("Executing schedule: " + schedule.Name);

        // can be used to pass values between commands
        var state = new Dictionary<string, object> {
            ["schedule"] = schedule,
            ["locomotive"] = locomotive,
            ["wage"] = 0
        };

        for (var i = firstCommand; i < schedule.Commands.Count; i++) {
			Messenger.Default.Send(new CommandIndexChanged { CommandIndex = i });

            locomotive.KeyValueObject["ScheduleRunner:CurrentCommand"] = schedule.Commands[i].DisplayText;

            state["index"] = i;
            var command = schedule.Commands[i]!;
            
            _Logger.Information("Executing command: " + command.DisplayText);

            SchedulerPlugin.DebugMessage($"AI Engineer [{Hyperlink.To(locomotive)}] {command.DisplayText}");

            var manager = ScheduleCommands.GetManager(command.GetType());
            manager.Command = command;

            var wait = manager.Execute(state);
            while (wait.MoveNext()) {
                yield return wait.Current;
            }

            if (state.TryGetValue("stop", out var value) && (bool)value) {
                global::UI.Console.Console.shared!.AddLine($"AI Engineer [{Hyperlink.To(locomotive)}] Schedule execution canceled.");
                break;
            }
        }

        locomotive.KeyValueObject["ScheduleRunner:CurrentCommand"] = "";
        var wage = (int)state["wage"];

        var entityReference = new EntityReference(EntityType.Car, locomotive.id!);
        StateManager.Shared.ApplyToBalance(-wage, Ledger.Category.WagesAI, entityReference, "Scheduler: " + schedule.Name);

        NoticeManager.Shared.PostEphemeral(entityReference, "Scheduler", "Completed");
    }
}
