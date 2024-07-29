namespace Scheduler.Data;

using System.Collections.Generic;

public sealed class Schedule {

    public string Name { get; set; } = null!;

    public List<IScheduleCommand> Commands { get; } = new();

    public Schedule Clone() {
        var schedule = new Schedule { Name = Name };
        foreach (var command in Commands) {
            schedule.Commands.Add(command.Clone());
        }

        return schedule;
    }
}