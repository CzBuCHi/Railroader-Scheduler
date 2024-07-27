namespace Scheduler.Data;

using System;
using Core;
using UnityEngine;

public enum ScheduleCommandType {

    MOVE,               // move train (yard / roam mode)
    CONNECT_AIR,        // connect air on all cars
    RELEASE_HANDBRAKES, // release handbrake on all cars
    SET_SWITCH,         // set switch direction
    UNCOUPLE,           // uncouple nth car
    SET_HANDBRAKE,      // set handbrake on nth car
    RESTORE_SWITCH      // restore state of switch from before SET_SWITCH command call

}

public sealed class ScheduleCommand {

    public static ScheduleCommand Move(bool forward, int? maxSpeed, float distance) {
        return new ScheduleCommand(ScheduleCommandType.MOVE, forward, maxSpeed, distance);
    }

    public static ScheduleCommand ConnectAir() {
        return new ScheduleCommand(ScheduleCommandType.CONNECT_AIR);
    }

    public static ScheduleCommand ReleaseHandbrakes() {
        return new ScheduleCommand(ScheduleCommandType.RELEASE_HANDBRAKES);
    }

    public static ScheduleCommand SetSwitch(bool forward, bool switchToNormal) {
        return new ScheduleCommand(ScheduleCommandType.SET_SWITCH, forward: forward, switchToNormal: switchToNormal);
    }

    public static ScheduleCommand Uncouple(int index) {
        return new ScheduleCommand(ScheduleCommandType.UNCOUPLE, index: index);
    }

    public static ScheduleCommand SetHandbrake(int index) {
        return new ScheduleCommand(ScheduleCommandType.SET_HANDBRAKE, index: index);
    }

    public static ScheduleCommand RestoreSwitch(bool forward) {
        return new ScheduleCommand(ScheduleCommandType.RESTORE_SWITCH, forward);
    }

    private ScheduleCommand(ScheduleCommandType commandType, bool? forward = null, int? maxSpeed = null, float? distance = null, bool? switchToNormal = null, int? index = null) {
        CommandType = commandType;
        Forward = forward;
        MaxSpeed = maxSpeed;
        Distance = distance;
        SwitchToNormal = switchToNormal;
        CarIndex = index;
    }

    public ScheduleCommandType CommandType { get; }

    // MOVE
    public bool? Forward { get; }
    public int? MaxSpeed { get; } // null = yard mode
    public float? Distance { get; }

    // SET_SWITCH
    public bool? SwitchToNormal { get; } // set switch to normal

    // UNCOUPLE, SET_HANDBRAKE
    public int? CarIndex { get; } // car index: 0 = first, 1 = second, -1 = last, -2, second from end

    public override string ToString() {
        return CommandType switch {
                   ScheduleCommandType.CONNECT_AIR        => "Connect air",
                   ScheduleCommandType.MOVE               => MoveToString(),
                   ScheduleCommandType.RELEASE_HANDBRAKES => "Release handbrakes",
                   ScheduleCommandType.SET_SWITCH         => $"Set {(Forward!.Value ? "front" : "back")} switch to {(SwitchToNormal!.Value ? "Normal" : "Reverse")}",
                   ScheduleCommandType.UNCOUPLE           => $"Uncouple car #{CarIndex}",
                   ScheduleCommandType.SET_HANDBRAKE      => $"Set handbrake on car #{CarIndex}",
                   ScheduleCommandType.RESTORE_SWITCH     => $"Restore {(Forward!.Value ? "front" : "back")} switch",
                   _                                      => throw new ArgumentOutOfRangeException()
               };

        string MoveToString() {
            var carCount = Mathf.FloorToInt(Distance!.Value / 12.2f);
            return $"Move  {carCount} {"car".Pluralize(carCount)} {(Forward!.Value ? "forward" : "back")} ({(MaxSpeed == null ? "Yard Speed" : MaxSpeed + " MPH")})";
        }
    }

    public override int GetHashCode() {
        unchecked {
            var hashCode = (int)CommandType;
            hashCode = (hashCode * 397) ^ Forward.GetHashCode();
            hashCode = (hashCode * 397) ^ MaxSpeed.GetHashCode();
            hashCode = (hashCode * 397) ^ Distance.GetHashCode();
            hashCode = (hashCode * 397) ^ SwitchToNormal.GetHashCode();
            hashCode = (hashCode * 397) ^ (CarIndex ?? 0);
            return hashCode;
        }
    }
}