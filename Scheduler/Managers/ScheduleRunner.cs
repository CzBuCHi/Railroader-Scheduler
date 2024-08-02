using System;
using System.Collections;
using Model;
using Scheduler.Data;
using UnityEngine;

namespace Scheduler.Managers;

internal sealed class ScheduleRunner : MonoBehaviour
{
    public void ExecuteSchedule(Schedule schedule, BaseLocomotive locomotive) {
        StartCoroutine(ExecuteCoroutine(schedule, locomotive));
    }

    private static IEnumerator ExecuteCoroutine(Schedule schedule, BaseLocomotive locomotive) {
        foreach (var command in schedule.Commands) {
            SchedulerPlugin.DebugMessage($"AI Engineer [{Hyperlink.To(locomotive)}] Executing {command}");

            try {
                command.Execute(locomotive);
            } catch (Exception e) {
                global::UI.Console.Console.shared!.AddLine($"AI Engineer [{Hyperlink.To(locomotive)}]: Pee in the cup moment: " + e.Message);
                throw;
            }

            yield return command.WaitBefore();
            yield return command.WaitUntilComplete(locomotive);
        }
    }
}
