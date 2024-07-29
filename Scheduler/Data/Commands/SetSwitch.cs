namespace Scheduler.Data.Commands;

using global::UI.Builder;
using Model;
using Newtonsoft.Json;
using Track;

public sealed class ScheduleCommandSetSwitch(bool front, bool isThrown) : IScheduleCommand {

    public string Identifier => "Set Switch";

    public bool Front { get; } = front;
    public bool IsThrown { get; } = isThrown;

    public override string ToString() {
        return $"Set {(Front ? "front" : "back")} switch to {(IsThrown ? "Reverse" : "Normal")}";
    }

    public void Execute(BaseLocomotive locomotive) {
        var startLocation = SchedulerUtility.StartLocation(Front, locomotive);
        var node = SchedulerUtility.GetNextSwitchByLocation(startLocation);

        SchedulerPlugin.DebugMessage($"NODE: {node.id}, Segment: {startLocation.segment.id}");

        // if closest switch is fouled my train, find next switch
        if (Graph.Shared.DecodeSwitchAt(node, out var enter, out _, out _) && startLocation.segment != enter) {
            var distance = startLocation.DistanceTo(node);
            var foulingDistance = Graph.Shared.CalculateFoulingDistance(node);
            if (distance < foulingDistance) {
                SchedulerPlugin.DebugMessage($"Fouling switch at {node.id}");
                node = SchedulerUtility.GetNextSwitch(node, enter);
            }
        }

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

public sealed class ScheduleCommandSetSwitchSerializer : ScheduleCommandSerializerBase<ScheduleCommandSetSwitch> {

    private bool? _Front;
    private bool? _IsThrown;

    protected override void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer) {
        switch (propertyName) {
            case "Front":
                _Front = reader.ReadAsBoolean();
                break;

            case "IsThrown":
                _IsThrown = reader.ReadAsBoolean();
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

public sealed class ScheduleCommandSetSwitchPanelBuilder : ScheduleCommandPanelBuilderBase {

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