using System.Linq;

namespace Scheduler.Data.Commands;

using System.Collections.Generic;
using global::UI.Builder;
using Model;
using Newtonsoft.Json;
using Track;

public sealed class ScheduleCommandSetSwitch(bool front, bool isThrown) : IScheduleCommand
{
    public string Identifier => "Set Switch";

    public bool Front { get; } = front;
    public bool IsThrown { get; } = isThrown;

    public override string ToString() {
        return $"Set {(Front ? "front" : "back")} switch to {(IsThrown ? "Reverse" : "Normal")}";
    }

    public void Execute(BaseLocomotive locomotive) {
        var startLocation = SchedulerUtility.FirstCarLocation(locomotive, Front ? Car.End.F : Car.End.R);

        var firstSwitch = true;
        var items = new List<(TrackSegment Segment, TrackNode Node)>();
        foreach (var item in SchedulerUtility.GetRoute(startLocation)) {
            items.Add(item);
            if (!Graph.Shared!.IsSwitch(item.Node)) {
                continue;
            }

            
            var distance = SchedulerUtility.Distance(startLocation, items);
            var (_, lastNode) = items.Last();
            var foulingDistance = Graph.Shared.CalculateFoulingDistance(lastNode);
            if (distance > foulingDistance || !firstSwitch) {
                break;
            }
            
            firstSwitch = false;
        }

        var (_, node) = items.Last();

        if (!SchedulerUtility.CanOperateSwitch(node, startLocation, locomotive)) {
            return;
        }

        SchedulerPlugin.DebugMessage($"NODE: {node.id}");
        SchedulerPlugin.Settings.SwitchStates[node.id] = node.isThrown;
        node.isThrown = IsThrown;
    }

    public IScheduleCommand Clone() {
        return new ScheduleCommandSetSwitch(Front, IsThrown);
    }
}

public sealed class ScheduleCommandSetSwitchSerializer : ScheduleCommandSerializerBase<ScheduleCommandSetSwitch>
{
    private bool? _Front;
    private bool? _IsThrown;

    protected override void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer) {
        switch (propertyName) {
            case "Front":
                _Front = serializer.Deserialize<bool>(reader);
                break;

            case "IsThrown":
                _IsThrown = serializer.Deserialize<bool>(reader);
                break;
        }
    }

    protected override ScheduleCommandSetSwitch BuildScheduleCommand() {
        ThrowIfNull(_Front, "Front");
        ThrowIfNull(_IsThrown, "IsThrown");

        return new ScheduleCommandSetSwitch(_Front!.Value, _IsThrown!.Value);
    }


    public override void Write(JsonWriter writer, ScheduleCommandSetSwitch value) {
        writer.WritePropertyName("Front");
        writer.WriteValue(value.Front);
        writer.WritePropertyName("IsThrown");
        writer.WriteValue(value.IsThrown);
    }
}

public sealed class ScheduleCommandSetSwitchPanelBuilder : ScheduleCommandPanelBuilderBase
{
    private bool _Front;
    private bool _IsThrown;

    public override void BuildPanel(UIPanelBuilder builder) {
        builder.AddField("Location",
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Front of train", _Front, () => SetToggle(ref _Front, true));
                strip.AddButtonSelectable("Rear of train", !_Front, () => SetToggle(ref _Front, false));
            })!
        );
        builder.AddField("Orientation",
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Normal", !_IsThrown, () => SetToggle(ref _IsThrown, false));
                strip.AddButtonSelectable("Reversed", _IsThrown, () => SetToggle(ref _IsThrown, true));
            })!
        );
        return;

        void SetToggle(ref bool field, bool value) {
            if (field == value) {
                return;
            }

            field = value;
            builder.Rebuild();
        }
    }

    public override IScheduleCommand CreateScheduleCommand() {
        return new ScheduleCommandSetSwitch(_Front, _IsThrown);
    }
}