using System.Collections.Generic;
using Scheduler.Data;

namespace Scheduler;

internal class Settings
{
    public bool Debug { get; set; }

    public List<Schedule> Schedules { get; set; } = new();
}
