namespace Scheduler.Data;

using Model;
using Newtonsoft.Json;

/// <summary> Command for scheduler to execute </summary>
[JsonConverter(typeof(ScheduleCommandConverter))]
public interface IScheduleCommand {

    string Identifier { get; }

    void Execute(BaseLocomotive locomotive);

    IScheduleCommand Clone();
}