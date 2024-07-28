namespace Scheduler.Data;

using System.Collections.Generic;

public sealed class Schedule {

    public string Name { get; set; } = null!;

    public List<IScheduleCommand> Commands { get; } = new();

}