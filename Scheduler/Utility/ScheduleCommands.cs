using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Scheduler.Commands;

namespace Scheduler.Utility;

public static class ScheduleCommands
{
    static ScheduleCommands() {
        Register<ConnectAir, ConnectAirManager>();
        Register<ReleaseHandbrakes, ReleaseHandbrakesManager>();
        Register<SetHandbrake, SetHandbrakeManager>();
        Register<Uncouple, UncoupleManager>();
        Register<RestoreSwitches, RestoreSwitchesManager>();
        Register<SetSwitch, SetSwitchManager>();
        Register<Wait, WaitManager>();
        Register<Move, MoveManager>();
        Register<NoticeWait, NoticeWaitManager>();

        

        Register<DeserializationFailed, DeserializationFailedManager>();
    }

    private static readonly Dictionary<Type, Type> _Managers = new();
    private static readonly Dictionary<Type, CommandManager> _Instances = new();

    public static void Register<TCommand, TCommandManager>() where TCommandManager : CommandManager<TCommand> where TCommand : ICommand {
        _Managers.Add(typeof(TCommand), typeof(TCommandManager));
        _CommandTypes = null;
    }

    private static Type[]? _CommandTypes;
    private static Type[] CommandTypes => _CommandTypes ??= _Managers.Keys.Where(o => o != typeof(DeserializationFailed)).OrderBy(o => o.Name).ToArray();

    private static readonly Regex _CamelToSpaceSeparated = new("(\\B[A-Z])", RegexOptions.Compiled);

    internal static List<string> Commands => CommandTypes.Select(o => _CamelToSpaceSeparated.Replace(o.Name, " $1")).ToList();

    internal static CommandManager GetManager(Type commandType) {
        if (!_Instances.TryGetValue(commandType, out var manager)) {
            _Managers.TryGetValue(commandType, out var managerType);
            manager = (CommandManager)Activator.CreateInstance(managerType!)!;
            _Instances[commandType] = manager;
        }

        return manager!;
    }

    internal static CommandManager GetManager(int index) {
        var commandType = CommandTypes[index];
        return GetManager(commandType);
    }

    public static int GetManagerIndex(ICommand command) {
        var commandType = command.GetType();
        return Array.IndexOf(CommandTypes, commandType);
    }
}
