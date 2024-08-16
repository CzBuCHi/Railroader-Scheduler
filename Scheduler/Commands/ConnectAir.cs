using System.Collections;
using System.Collections.Generic;
using Game.Messages;
using Game.State;
using Model;
using Scheduler.HarmonyPatches;
using Scheduler.Utility;

namespace Scheduler.Commands;

/// <summary> Connect air on train. </summary>
public sealed class ConnectAir : ICommand
{
    public string DisplayText => "Connect Air";
}

public sealed class ConnectAirManager : CommandManager<ConnectAir>
{
    public override IEnumerator Execute(Dictionary<string, object> state) {
        var locomotive = (BaseLocomotive)state["locomotive"]!;

        foreach (var car in locomotive.set!.Cars!) {
            ConnectAirOnEnd(car, Car.LogicalEnd.A);
            ConnectAirOnEnd(car, Car.LogicalEnd.B);
        }

        yield break;

        static void ConnectAirOnEnd(Car car, Car.LogicalEnd end) {
            StateManager.ApplyLocal(new PropertyChange(car.id!, CarPatches.KeyValueKeyFor(Car.EndGearStateKey.Anglecock, car.LogicalToEnd(end)), new FloatPropertyValue(car[end]!.IsCoupled ? 1f : 0f)));

            if (car.TryGetAdjacentCar(end, out var adjacent)) {
                StateManager.ApplyLocal(new SetGladhandsConnected(car.id!, adjacent!.id!, true));
            }
        }
    }

    public override ICommand CreateCommand() {
        return new ConnectAir();
    }
}
