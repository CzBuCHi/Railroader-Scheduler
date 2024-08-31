using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Scheduler.Commands;
using Scheduler.Utility;

namespace Scheduler.Data;

public sealed class Schedule
{
    public string Name { get; set; } = null!;

    public List<ICommand> Commands { get; } = new();

    public bool IsValid => Commands.Any(o => o is DeserializationFailed);

    public Schedule Clone() {
        var json = JsonConvert.SerializeObject(this);
        return JsonConvert.DeserializeObject<Schedule>(json)!;
    }
}
