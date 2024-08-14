using System.Collections.Generic;
using Newtonsoft.Json;
using Scheduler.Utility;

namespace Scheduler.Data;

public sealed class Schedule
{
    public string Name { get; set; } = null!;

    public List<ICommand> Commands { get; } = new();

    public Schedule Clone() {
        var schedule = new Schedule { Name = Name };
        foreach (var command in Commands) {
            var json = JsonConvert.SerializeObject(command);
            var clone = JsonConvert.DeserializeObject<ICommand>(json)!;
            schedule.Commands.Add(clone);
        }

        return schedule;
    }
}
