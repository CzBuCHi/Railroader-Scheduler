using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Scheduler.Commands;

namespace Scheduler.Utility;

public static class ScheduleCommands
{
    static ScheduleCommands()
    {
        Register<ConnectAir, ConnectAirManager>();
        Register<ReleaseHandbrakes, ReleaseHandbrakesManager>();
        Register<SetHandbrake, SetHandbrakeManager>();
        Register<Uncouple, UncoupleManager>();
    }

    private static readonly Dictionary<Type, Type> _Managers = new();

    public static void Register<TCommand, TCommandManager>() where TCommandManager : CommandManager<TCommand> where TCommand : ICommand
    {
        _Managers.Add(typeof(TCommand), typeof(TCommandManager));
    }

    private static readonly Regex _CamelToSpaceSeparated = new("(\\B[A-Z])", RegexOptions.Compiled);

    internal static List<string> Commands => _Managers.Keys.Select(o => _CamelToSpaceSeparated.Replace(o.Name, " $1")).ToList();

    internal static CommandManager<TCommand> GetManager<TCommand>(TCommand command) where TCommand : ICommand
    {
        _Managers.TryGetValue(typeof(TCommand), out var managerType);
        var manager = (CommandManager<TCommand>)Activator.CreateInstance(managerType);
        manager.Command = command;
        return manager;
    }

    internal static CommandManager GetManager(int index) {
        var key = _Managers.Keys.ToArray()[index];
        var managerType = _Managers[key];
        return (CommandManager)Activator.CreateInstance(managerType);
    }
}
