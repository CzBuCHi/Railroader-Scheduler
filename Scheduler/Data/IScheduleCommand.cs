using Model;
using Newtonsoft.Json;
using UnityEngine;

namespace Scheduler.Data;

/// <summary> Command for scheduler to execute </summary>
[JsonConverter(typeof(ScheduleCommandConverter))]
public interface IScheduleCommand
{
    /// <summary> Command identifier </summary>
    string Identifier { get; }

    /// <summary> Execute given command on <paramref name="locomotive"/>. </summary>
    void Execute(BaseLocomotive locomotive);

    /// <summary> Wait before checking if command completed. </summary>
    CustomYieldInstruction WaitBefore();

    /// <summary> Wait until command completes. </summary>
    WaitUntil? WaitUntilComplete(BaseLocomotive locomotive);

    /// <summary> Clone this command. </summary>
    IScheduleCommand Clone();
}
