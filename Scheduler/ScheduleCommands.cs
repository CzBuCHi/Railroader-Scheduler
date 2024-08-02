using System.Collections.Generic;
using System.Linq;
using Scheduler.Commands;
using Scheduler.Data;

namespace Scheduler;

public static class ScheduleCommands
{
    static ScheduleCommands()
    {
        RegisterSerializer<ScheduleCommandConnectAir, ScheduleCommandConnectAirSerializer, ScheduleCommandConnectAirPanelBuilder>("Connect Air");
        RegisterSerializer<ScheduleCommandReleaseHandbrakes, ScheduleCommandReleaseHandbrakesSerializer, ScheduleCommandReleaseHandbrakesPanelBuilder>("Release Handbrakes");
        RegisterSerializer<ScheduleCommandUncouple, ScheduleCommandUncoupleSerializer, ScheduleCommandUncouplePanelBuilder>("Uncouple");
        RegisterSerializer<ScheduleCommandSetHandbrake, ScheduleCommandSetHandbrakeSerializer, ScheduleCommandSetHandbrakePanelBuilder>("Set Handbrake");
        RegisterSerializer<ScheduleCommandSetSwitch, ScheduleCommandSetSwitchSerializer, ScheduleCommandSetSwitchPanelBuilder>("Set Switch");
        RegisterSerializer<ScheduleCommandRestoreSwitch, ScheduleCommandRestoreSwitchSerializer, ScheduleCommandRestoreSwitchPanelBuilder>("Restore Switch");
        RegisterSerializer<ScheduleCommandMove, ScheduleCommandMoveSerializer, ScheduleCommandMovePanelBuilder>("Move");
    }

    internal static List<string> Commands => _Serializer.Keys.ToList();

    internal static List<IScheduleCommandPanelBuilder> CommandPanelBuilders => _Serializer.Values.Select(o => o.PanelBuilder).ToList();

    private static readonly Dictionary<string, (IScheduleCommandSerializer Serializer, IScheduleCommandPanelBuilder PanelBuilder)> _Serializer = new();

    public static void RegisterSerializer<TScheduleCommand, TScheduleCommandSerializer, TScheduleCommandPanelBuilder>(string identifier)
        where TScheduleCommand : IScheduleCommand
        where TScheduleCommandSerializer : IScheduleCommandSerializer<TScheduleCommand>, new()
        where TScheduleCommandPanelBuilder : IScheduleCommandPanelBuilder, new()
    {
        _Serializer.Add(identifier, (new TScheduleCommandSerializer(), new TScheduleCommandPanelBuilder()));
    }

    internal static IScheduleCommandSerializer? FindSerializer(string identifier)
    {
        if (!_Serializer.TryGetValue(identifier, out var value))
        {
            return null;
        }

        return value.Serializer;
    }
}
