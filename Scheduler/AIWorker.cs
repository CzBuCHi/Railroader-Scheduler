namespace Scheduler;

using System;
using System.Collections.Generic;
using System.Linq;
using Game.Messages;
using Game.State;
using global::UI.EngineControls;
using HarmonyLib;
using Model;
using Model.AI;
using Scheduler.Data;
using Scheduler.HarmonyPatches;
using Track;

public static class AIWorker {

    public static void ExecuteCommand(ScheduleCommand command, BaseLocomotive locomotive) {
        switch (command.CommandType) {
            case ScheduleCommandType.MOVE:
                Move(locomotive, command.Forward, command.MaxSpeed, command.Distance);
                break;

            case ScheduleCommandType.CONNECT_AIR:
                ConnectAir(locomotive.set!.Cars!);
                break;

            case ScheduleCommandType.RELEASE_HANDBRAKES:
                ReleaseAllHandbrakes(locomotive.set!.Cars!);
                break;

            case ScheduleCommandType.SET_SWITCH: {
                var startLocation = StartLocation(command.Forward!.Value, locomotive);
                var node = GetNextSwitchByLocation(startLocation);
                SetSwitch(node, command.SwitchToNormal!.Value);
                break;
            }

            case ScheduleCommandType.UNCOUPLE:
                Uncouple(locomotive, command.CarIndex!.Value);
                break;

            case ScheduleCommandType.SET_HANDBRAKE:
                SetHandbrake(locomotive, command.CarIndex!.Value);
                break;

            case ScheduleCommandType.RESTORE_SWITCH: {
                var startLocation = StartLocation(command.Forward!.Value, locomotive);
                var node = GetNextSwitchByLocation(startLocation);
                RestoreSwitch(node);
                break;
            }

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void Move(BaseLocomotive locomotive, bool? forward, int? maxSpeed, float? distance) {
        var persistence = new AutoEngineerPersistence(locomotive.KeyValueObject!);
        var helper = new AutoEngineerOrdersHelper(locomotive, persistence);
        helper.SetOrdersValue(maxSpeed == null ? AutoEngineerMode.Yard : AutoEngineerMode.Road, forward, maxSpeed, distance);
    }

    private static void ConnectAir(IEnumerable<Car> consist)
    {
        foreach (var car in consist)
        {
            ConnectAirCore(car, Car.LogicalEnd.A);
            ConnectAirCore(car, Car.LogicalEnd.B);
        }

        return;

        static void ConnectAirCore(Car car, Car.LogicalEnd end)
        {
            StateManager.ApplyLocal(new PropertyChange(car.id!, CarPatches.KeyValueKeyFor(Car.EndGearStateKey.Anglecock, car.LogicalToEnd(end)), new FloatPropertyValue(car[end]!.IsCoupled ? 1f : 0f)));

            if (car.TryGetAdjacentCar(end, out var car2))
            {
                StateManager.ApplyLocal(new SetGladhandsConnected(car.id!, car2!.id!, true));
            }
        }
    }

    private static void ReleaseAllHandbrakes(IEnumerable<Car> consist)
    {
        consist.Do(c => c.SetHandbrake(false));
    }

    private static void SetSwitch(TrackNode node, bool desiredState) {
        SchedulerPlugin.Settings.SwitchStates[node.id] = node.isThrown;
        node.isThrown = desiredState;
    }

    private static void Uncouple(BaseLocomotive locomotive, int carIndex) {
        var cars = locomotive.set!.Cars!.ToArray();

        if (cars.Length < carIndex) {
            return;
        }

        var carToDisconnect = cars[carIndex]!;
        var newEndCar = cars[carIndex + 1]!;

        var newEndCarEndToDisconnect = newEndCar.CoupledTo(Car.LogicalEnd.A) == carToDisconnect ? Car.LogicalEnd.A : Car.LogicalEnd.B;
        var carToDisconnectEndToDisconnect = carToDisconnect.CoupledTo(Car.LogicalEnd.A) == newEndCar ? Car.LogicalEnd.A : Car.LogicalEnd.B;

        newEndCar.ApplyEndGearChange(newEndCarEndToDisconnect, Car.EndGearStateKey.CutLever, 1f);
        newEndCar.ApplyEndGearChange(newEndCarEndToDisconnect, Car.EndGearStateKey.Anglecock, 0f);
        carToDisconnect.ApplyEndGearChange(carToDisconnectEndToDisconnect, Car.EndGearStateKey.Anglecock, 0f);
    }

    private static void SetHandbrake(BaseLocomotive locomotive, int carIndex) {

        var cars = locomotive.set!.Cars!;
        if (carIndex > 0) {
            cars = cars.Skip(carIndex - 1);
        }

        var car = cars.First();
        car.SetHandbrake(true);
    }

    private static void RestoreSwitch(TrackNode node) {
        if (SchedulerPlugin.Settings.SwitchStates.TryGetValue(node.id, out var state)) {
            node.isThrown = state;
        }
    }

    private static Location StartLocation(bool forward, BaseLocomotive baseLocomotive) {
        var end = forward ? Car.End.F : Car.End.R;
        var logical = baseLocomotive.EndToLogical(end);
        var car = baseLocomotive.EnumerateCoupled(logical).First<Car>();
        var num = logical == Car.LogicalEnd.A ? 1 : 0;
        var location = num != 0 ? car.LocationA : car.LocationB;
        return num == 0 ? location.Flipped() : location;
    }

    private static TrackNode GetNextSwitchByLocation(Location startLocation) {
        var graph = TrainController.Shared.graph;
        var trackSegment1 = startLocation.segment;
        var end1 = startLocation.EndIsA ? TrackSegment.End.B : TrackSegment.End.A;
        var node = trackSegment1.NodeForEnd(end1);
        for (var index = 0; index < 50; ++index) {
            if (graph.NodeIsDeadEnd(node, out var _)) {
                return node;
            }

            if (graph.IsSwitch(node)) {
                return node;
            }

            var segment = trackSegment1;
            foreach (var trackSegment2 in graph.SegmentsConnectedTo(node)) {
                if (!(trackSegment2 == trackSegment1)) {
                    segment = trackSegment2;
                }
            }

            if (segment != null) {
                var end2 = new Location(segment, 0.0f, segment.a == node ? TrackSegment.End.A : TrackSegment.End.B).EndIsA ? TrackSegment.End.B : TrackSegment.End.A;
                trackSegment1 = segment;
                node = trackSegment1.NodeForEnd(end2);
            }
        }

        return node;
    }
    
}