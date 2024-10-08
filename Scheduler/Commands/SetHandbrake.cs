﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Model;
using Newtonsoft.Json;
using Scheduler.Extensions;
using Scheduler.Utility;
using UI.Builder;

namespace Scheduler.Commands;

/// <summary> Sets handbrake on given car. </summary>
/// <param name="carIndex">Car index counted from locomotive.</param>
public sealed class SetHandbrake(int carIndex) : ICommand
{
    public string DisplayText => $"Set handbrake on {CarIndex.GetRelativePosition()}";

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
            yield break;
        }

        car!.SetHandbrake(true);
        state["wage"] = (int)state["wage"] + 1;
    }

    private int? _CarIndex;

    public override void SerializeProperties(JsonWriter writer) {
        Logger.Information("SetHandbrakeManager:SerializeProperties");
        writer.WritePropertyName(nameof(SetHandbrake.CarIndex));
        writer.WriteValue(Command!.CarIndex);
    }

    protected override void ReadProperty(string propertyName, JsonReader reader, JsonSerializer serializer) {
        Logger.Information("SetHandbrakeManager:ReadProperty '" + propertyName + "'");
        if (propertyName == nameof(SetHandbrake.CarIndex)) {
            _CarIndex = serializer.Deserialize<int>(reader);
        }
    }

    protected override object TryCreateCommand() {
        if (_CarIndex == null) {
            return "Missing mandatory property 'CarIndex'.";
        }

        return new SetHandbrake(_CarIndex!.Value);
    }

    private int _DropdownIndex = -1;

    public override void BuildPanel(UIPanelBuilder builder, BaseLocomotive locomotive) {
        SchedulerUtility.ResolveTrainCars(locomotive, out var trainCars, out var trainCarsPositions, true);
        if (_DropdownIndex == -1) {
            _DropdownIndex = 0;
            _CarIndex = trainCarsPositions[0];
        }

        builder.AddField("Car index",
            builder.AddDropdown(trainCars, _DropdownIndex, o => {
                _DropdownIndex = o;
                _CarIndex = trainCarsPositions[o];
                builder.Rebuild();
            })!
        );
    }
}
