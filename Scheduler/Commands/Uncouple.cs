using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Model;
using Newtonsoft.Json;
using Scheduler.Extensions;
using Scheduler.Utility;
using UI.Builder;

namespace Scheduler.Commands;

/// <summary> Uncouple given car. </summary>
/// <param name="carIndex">Car index counted from locomotive.</param>
public sealed class Uncouple(int carIndex) : ICommand
{
    public string DisplayText => $"Uncouple {CarIndex.GetRelativePosition()}";

    public int CarIndex { get; } = carIndex;
}

public sealed class UncoupleManager : CommandManager<Uncouple>
{
    public override IEnumerator Execute(Dictionary<string, object> state) {
      

        var locomotive = (BaseLocomotive)state["locomotive"]!;
        if (Command!.CarIndex == 0) {
            locomotive.SetHandbrake(true);
            yield break;
        }

        var consist = locomotive.EnumerateConsist();
        var cars = consist.ToDictionary(o => o.Position, o => o.Car);

        var carIndex = Command.CarIndex;

        Car carToDisconnect;
        Car newEndCar;
        if (carIndex > 0) {
            if (!cars.TryGetValue(carIndex - 1, out carToDisconnect)) {
                yield break;
            }

            if (!cars.TryGetValue(carIndex, out newEndCar)) {
                yield break;
            }
        } else {
            if (!cars.TryGetValue(carIndex, out carToDisconnect)) {
                yield break;
            }

            if (!cars.TryGetValue(carIndex + 1, out newEndCar)) {
                yield break;
            }
        }
        
        var newEndCarEndToDisconnect = newEndCar!.CoupledTo(Car.LogicalEnd.A) == carToDisconnect ? Car.LogicalEnd.A : Car.LogicalEnd.B;
        var carToDisconnectEndToDisconnect = carToDisconnect!.CoupledTo(Car.LogicalEnd.A) == newEndCar ? Car.LogicalEnd.A : Car.LogicalEnd.B;

        carToDisconnect.ApplyEndGearChange(carToDisconnectEndToDisconnect, Car.EndGearStateKey.IsAirConnected, false);
        carToDisconnect.ApplyEndGearChange(carToDisconnectEndToDisconnect, Car.EndGearStateKey.Anglecock, 0f);

        newEndCar.ApplyEndGearChange(newEndCarEndToDisconnect, Car.EndGearStateKey.IsAirConnected, false);
        newEndCar.ApplyEndGearChange(newEndCarEndToDisconnect, Car.EndGearStateKey.Anglecock, 0f);

        newEndCar.ApplyEndGearChange(newEndCarEndToDisconnect, Car.EndGearStateKey.CutLever, 1f);
        state["wage"] = (int)state["wage"] + 1;
    }

    private int? _CarIndex;

    public override void SerializeProperties(JsonWriter writer) {
        writer.WritePropertyName(nameof(Uncouple.CarIndex));
        writer.WriteValue(Command!.CarIndex);
    }

    protected override void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer) {
        if (propertyName == nameof(Uncouple.CarIndex)) {
            _CarIndex = serializer.Deserialize<int>(reader);
        }
    }

    protected override object TryCreateCommand() {
        if (_CarIndex == null) {
            return "Missing mandatory property 'CarIndex'.";
        }

        return new Uncouple(_CarIndex!.Value);
    }

    private int _DropdownIndex;

    public override void BuildPanel(UIPanelBuilder builder, BaseLocomotive locomotive) {
        SchedulerUtility.ResolveTrainCars(locomotive, out var trainCars, out var trainCarsPositions, false);
        builder.AddField("Car index",
            builder.AddDropdown(trainCars, _DropdownIndex, o => {
                _DropdownIndex = o;
                _CarIndex = trainCarsPositions[o];
                builder.Rebuild();
            })!
        );
    }
}
