namespace Scheduler;

using System.Collections.Generic;
using Scheduler.Data;

public class Settings {

    public List<Schedule> Schedules { get; set; } = new();

    public Dictionary<string, bool> SwitchStates { get; set; } = new();

}