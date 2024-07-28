namespace Scheduler;

using System.Collections.Generic;
using Scheduler.Data;

internal class Settings {

    public bool Debug { get; set; }

    public List<Schedule> Schedules { get; set; } = new();

    public Dictionary<string, bool> SwitchStates { get; set; } = new();

}