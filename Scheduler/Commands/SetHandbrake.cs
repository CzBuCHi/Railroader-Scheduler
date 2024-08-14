using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Model;
using Newtonsoft.Json;
using Scheduler.Extensions;
using Scheduler.Utility;
using UI.Builder;

namespace Scheduler.Commands;

public sealed class SetHandbrake(int carIndex) : ICommand
{
    public string DisplayText => $"Set handbrake on car #{CarIndex}";

    public int CarIndex { get; } = carIndex;
}

public sealed class SetHandbrakeManager : CommandManager<SetHandbrake>
{
    public override IEnumerator Execute(Dictionary<string, object> state) {
        var locomotive = (BaseLocomotive)state["locomotive"]!;

        if (Command!.CarIndex == 0) {
            locomotive.SetHandbrake(true);
            yield break;
        }

        var consist = locomotive.EnumerateConsist();
        var cars = consist.ToDictionary(o => o.Position, o => o.Car);

        if (!cars.TryGetValue(Command.CarIndex, out var car)) {
            // TODO: Error message?
            yield break;
        }

        car!.SetHandbrake(true);
    }

    private int? _CarIndex;

    public override void Serialize(JsonWriter writer) {
        writer.WritePropertyName("CarIndex");
        writer.WriteValue(Command!.CarIndex);
    }

    protected override void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer) {
        if (propertyName == "CarIndex") {
            _CarIndex = serializer.Deserialize<int>(reader);
        }
    }

    protected override SetHandbrake CreateCommandBase() {
        ThrowIfNull(_CarIndex, "CarIndex");
        return new SetHandbrake(_CarIndex!.Value);
    }

    private int _TrainCarsIndex;

    public override void BuildPanel(UIPanelBuilder builder, BaseLocomotive locomotive) {
        SchedulerUtility.ResolveTrainCars(locomotive, out var trainCars, out var trainCarsPositions);
        builder.AddField("Car index",
            builder.AddDropdown(trainCars, _TrainCarsIndex, o => {
                _TrainCarsIndex = o;
                _CarIndex = trainCarsPositions[o];
                builder.Rebuild();
            })!
        );
    }
}
