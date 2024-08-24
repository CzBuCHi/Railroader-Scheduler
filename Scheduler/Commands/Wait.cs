using System.Collections;
using System.Collections.Generic;
using Model;
using Newtonsoft.Json;
using Scheduler.Utility;
using UI.Builder;
using UnityEngine;

namespace Scheduler.Commands;

/// <summary> Wait given amount of <paramref name="milliSeconds"/> before continuing with next command in schedule. </summary>
/// <param name="milliSeconds">Number of milliseconds to wait.</param>
public sealed class Wait(float milliSeconds) : ICommand
{
    public string DisplayText => $"Wait for {MilliSeconds * 0.001f:0.###} seconds";

    public float MilliSeconds { get; } = milliSeconds;
}

public sealed class WaitManager : CommandManager<Wait>
{
    public override IEnumerator Execute(Dictionary<string, object> state) {
        return new WaitForSecondsRealtime(Command!.MilliSeconds);
    }

    private float? _MilliSeconds;

    public override void SerializeProperties(JsonWriter writer) {
        writer.WritePropertyName(nameof(Wait.MilliSeconds));
        writer.WriteValue(Command!.MilliSeconds);
    }

    protected override void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer) {
        if (propertyName == nameof(Wait.MilliSeconds)) {
            _MilliSeconds = serializer.Deserialize<float>(reader);
        }
    }

    protected override object TryCreateCommand() {
        if (_MilliSeconds == null) {
            return "Missing mandatory property 'MilliSeconds'.";
        }

        return new Wait(_MilliSeconds!.Value);
    }

    public override void BuildPanel(UIPanelBuilder builder, BaseLocomotive locomotive) {
        builder.AddField("Milliseconds", builder.AddSlider(() => _MilliSeconds ?? 0, () => (_MilliSeconds ?? 0).ToString("0"), o => _MilliSeconds = o, 0, 60 * 60 * 1000, true, o => _MilliSeconds = o)!);
    }
}
