using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Scheduler.Commands;
using Serilog;

namespace Scheduler.Utility;

public static class ScheduleCommands
{
    private static readonly ILogger _Logger = Log.ForContext(typeof(SchedulerPlugin))!;

    static ScheduleCommands() {
        Register<ConnectAir, ConnectAirManager>();
        Register<ReleaseHandbrakes, ReleaseHandbrakesManager>();
        Register<SetHandbrake, SetHandbrakeManager>();
        Register<Uncouple, UncoupleManager>();
        Register<RestoreSwitches, RestoreSwitchesManager>();
        Register<SetSwitch, SetSwitchManager>();
        Register<Wait, WaitManager>();
        Register<Move, MoveManager>();
    }

    private static readonly Dictionary<Type, Type> _Managers = new();
    private static readonly Dictionary<Type, CommandManager> _Instances = new();

    public static void Register<TCommand, TCommandManager>() where TCommandManager : CommandManager<TCommand> where TCommand : ICommand {
        _Logger.Information($"Register: {typeof(TCommand).Name}");
        _Managers.Add(typeof(TCommand), typeof(TCommandManager));
        _CommandTypes = null;
    }

    private static Type[]? _CommandTypes;
    private static Type[] CommandTypes => _CommandTypes ??= _Managers.Keys.OrderBy(o => o.Name).ToArray();

    private static readonly Regex _CamelToSpaceSeparated = new("(\\B[A-Z])", RegexOptions.Compiled);

    internal static List<string> Commands => CommandTypes.Select(o => _CamelToSpaceSeparated.Replace(o.Name, " $1")).ToList();

    internal static CommandManager GetManager(Type commandType) {
        _Logger.Information($"GetManager by type: {commandType.Name}");
        if (!_Instances.TryGetValue(commandType, out var manager)) {
            _Managers.TryGetValue(commandType, out var managerType);
            manager = (CommandManager)Activator.CreateInstance(managerType);
            _Instances[commandType] = manager;
        }

        return manager!;
    }

    internal static CommandManager GetManager(int index) {
        _Logger.Information($"GetManager by index: #{index}");
        var commandType = CommandTypes[index];
        return GetManager(commandType);
    }
}
