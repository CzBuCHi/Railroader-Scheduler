namespace Scheduler.Data.Commands;

using Game.Messages;
using Game.State;
using Model;
using Scheduler.HarmonyPatches;

public sealed class ScheduleCommandConnectAir : IScheduleCommand {

    public string Identifier => "Connect Air";

    public override string ToString() {
        return "Connect air";
    }

    public void Execute(BaseLocomotive locomotive) {
        foreach (var car in locomotive.set!.Cars!) {
            ConnectAirCore(car, Car.LogicalEnd.A);
            ConnectAirCore(car, Car.LogicalEnd.B);
        }

        return;

        static void ConnectAirCore(Car car, Car.LogicalEnd end) {
            StateManager.ApplyLocal(new PropertyChange(car.id!, CarPatches.KeyValueKeyFor(Car.EndGearStateKey.Anglecock, car.LogicalToEnd(end)), new FloatPropertyValue(car[end]!.IsCoupled ? 1f : 0f)));

            if (car.TryGetAdjacentCar(end, out var car2)) {
                StateManager.ApplyLocal(new SetGladhandsConnected(car.id!, car2!.id!, true));
            }
        }
    }

    public IScheduleCommand Clone() {
        return new ScheduleCommandConnectAir();
    }
}

public sealed class ScheduleCommandConnectAirSerializer : ScheduleCommandSerializerBase<ScheduleCommandConnectAir> {

}

public sealed class ScheduleCommandConnectAirPanelBuilder : ScheduleCommandPanelBuilderBase {

    public override IScheduleCommand CreateScheduleCommand() {
        return new ScheduleCommandConnectAir();
    }

}