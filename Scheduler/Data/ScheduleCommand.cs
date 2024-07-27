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

public sealed class ScheduleCommand(ScheduleCommandType commandType, bool? forward = null, int? maxSpeed = null, float? distance = null, bool? switchToNormal = null, int? index = null) {


    public ScheduleCommandType CommandType { get; } = commandType;

    // MOVE
    public bool? Forward { get; } = forward;
    public int? MaxSpeed { get; } = maxSpeed; // null = yard mode
    public float? Distance { get; } = distance;

    // SET_SWITCH
    public bool? SwitchToNormal { get; } = switchToNormal;

    // UNCOUPLE, SET_HANDBRAKE
    public int? CarIndex { get; } = index;

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