using System.Collections.Generic;
using System.Linq;
using Model;
using Newtonsoft.Json;
using Scheduler.Commands.Abstract;
using Scheduler.Data;
using Scheduler.Extensions;
using UI.Builder;

namespace Scheduler.Commands;

public sealed class ScheduleCommandSetHandbrake(int carIndex) : ScheduleCommandBase
{
    public override string Identifier => "Set Handbrake";

    public int CarIndex { get; } = carIndex;

    public override string ToString()
    {
        return $"Set handbrake on car #{CarIndex}";
    }

    public override void Execute(BaseLocomotive locomotive)
    {
        var consist = locomotive.EnumerateConsist();
        var cars = consist
                   .Where(o => o.Car != locomotive) // exclude locomotive
                   .ToDictionary(o => o.Position, o => o.Car);

        if (!cars.TryGetValue(CarIndex, out var car))
        {
            return;
        }

        car!.SetHandbrake(true);
    }

    public override IScheduleCommand Clone()
    {
        return new ScheduleCommandSetHandbrake(CarIndex);
    }
}

public sealed class ScheduleCommandSetHandbrakeSerializer : ScheduleCommandSerializerBase<ScheduleCommandSetHandbrake>
{
    private int? _CarIndex;

    protected override void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer)
    {
        if (propertyName == "CarIndex")
        {
            _CarIndex = serializer.Deserialize<int>(reader);
        }
    }

    protected override ScheduleCommandSetHandbrake BuildScheduleCommand()
    {
        ThrowIfNull(_CarIndex, "CarIndex");
        return new ScheduleCommandSetHandbrake(_CarIndex!.Value);
    }

    public override void Write(JsonWriter writer, ScheduleCommandSetHandbrake value)
    {
        writer.WritePropertyName("CarIndex");
        writer.WriteValue(value.CarIndex);
    }
}

public sealed class ScheduleCommandSetHandbrakePanelBuilder : ScheduleCommandPanelBuilderBase
{
    private List<string> _TrainCars = null!;
    private List<int> _TrainCarsPositions = null!;
    private int _TrainCarsIndex;


    public override void Configure(BaseLocomotive locomotive)
    {
        SchedulerUtility.ResolveTrainCars(locomotive, out _TrainCars, out _TrainCarsPositions);
    }

    public override void BuildPanel(UIPanelBuilder builder) {
        builder.AddField("Car index",
            builder.AddDropdown(_TrainCars, _TrainCarsIndex, o => {
                _TrainCarsIndex = o;
                builder.Rebuild();
            })!
        );
    }

    public override IScheduleCommand CreateScheduleCommand()
    {
        return new ScheduleCommandSetHandbrake(_TrainCarsPositions[_TrainCarsIndex]);
    }
}
