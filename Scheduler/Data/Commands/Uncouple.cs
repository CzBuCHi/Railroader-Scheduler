namespace Scheduler.Data.Commands;

using System.Collections.Generic;
using System.Linq;
using global::UI.Builder;
using Model;
using Newtonsoft.Json;
using Scheduler.Extensions;

public sealed class ScheduleCommandUncouple(int carIndex) : IScheduleCommand {

    public string Identifier => "Uncouple";

    public int CarIndex { get; } = carIndex;

    public override string ToString() {
        return $"Uncouple car #{CarIndex}";
    }

    public void Execute(BaseLocomotive locomotive) {
        var consist = locomotive.EnumerateConsist();
        var cars = consist
            .Where(o => o.Car != locomotive) // exclude locomotive
            .ToDictionary(o => o.Position, o => o.Car);

        var carIndex = CarIndex;

        Car carToDisconnect;
        Car newEndCar;
        int index;
        if (carIndex > 0) {
            index = carIndex - 1;

            if (!cars.TryGetValue(carIndex - 1, out carToDisconnect)) {
                return;
            }

            if (!cars.TryGetValue(carIndex, out newEndCar)) {
                return;
            }
        } else {
            index = carIndex;
            if (!cars.TryGetValue(carIndex, out carToDisconnect)) {
                return;
            }

            if (!cars.TryGetValue(carIndex + 1, out newEndCar)) {
                return;
            }
        }

        SchedulerPlugin.DebugMessage($"Uncoupling {carToDisconnect!.DisplayName} ({carToDisconnect.Archetype} | {index}).");
        
        var newEndCarEndToDisconnect = newEndCar!.CoupledTo(Car.LogicalEnd.A) == carToDisconnect ? Car.LogicalEnd.A : Car.LogicalEnd.B;
        var carToDisconnectEndToDisconnect = carToDisconnect!.CoupledTo(Car.LogicalEnd.A) == newEndCar ? Car.LogicalEnd.A : Car.LogicalEnd.B;

        carToDisconnect.ApplyEndGearChange(carToDisconnectEndToDisconnect, Car.EndGearStateKey.IsAirConnected, false);
        carToDisconnect.ApplyEndGearChange(carToDisconnectEndToDisconnect, Car.EndGearStateKey.Anglecock, 0f);

        newEndCar.ApplyEndGearChange(newEndCarEndToDisconnect, Car.EndGearStateKey.IsAirConnected, false);
        newEndCar.ApplyEndGearChange(newEndCarEndToDisconnect, Car.EndGearStateKey.Anglecock, 0f);

        newEndCar.ApplyEndGearChange(newEndCarEndToDisconnect, Car.EndGearStateKey.CutLever, 1f);
    }

    public IScheduleCommand Clone() {
        return new ScheduleCommandUncouple(CarIndex);
    }
}

public sealed class ScheduleCommandUncoupleSerializer : ScheduleCommandSerializerBase<ScheduleCommandUncouple> {

    private int? _CarIndex;

    protected override void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer) {
        if (propertyName == "CarIndex") {
            _CarIndex = serializer.Deserialize<int>(reader);
        }
    }

    protected override ScheduleCommandUncouple BuildScheduleCommand() {
        ThrowIfNull(_CarIndex, "CarIndex");

        return new ScheduleCommandUncouple(_CarIndex!.Value);
    }

    public override void Write(JsonWriter writer, ScheduleCommandUncouple value) {
        writer.WritePropertyName("CarIndex");
        writer.WriteValue(value.CarIndex);
    }

}

public sealed class ScheduleCommandUncouplePanelBuilder : ScheduleCommandPanelBuilderBase {

    private List<string> _TrainCars = null!;
    private List<int> _TrainCarsPositions = null!;
    private int _TrainCarsIndex;

    public override void Configure(BaseLocomotive locomotive) {
        SchedulerUtility.ResolveTrainCars(locomotive, out _TrainCars, out _TrainCarsPositions);
    }

    public override void BuildPanel(UIPanelBuilder builder) {
        builder.AddDropdown(_TrainCars, _TrainCarsIndex, o => {
            _TrainCarsIndex = o;
            builder.Rebuild();
        });
    }

    public override IScheduleCommand CreateScheduleCommand() {
        return new ScheduleCommandUncouple(_TrainCarsPositions[_TrainCarsIndex]);
    }

}