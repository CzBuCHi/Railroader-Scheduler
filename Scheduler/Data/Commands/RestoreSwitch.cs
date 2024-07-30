#region

using System.Collections.Generic;
using System.Linq;
using Model;
using Newtonsoft.Json;
using Track;
using UI.Builder;

#endregion

namespace Scheduler.Data.Commands;

public sealed class ScheduleCommandRestoreSwitch(bool front) : IScheduleCommand
{
    public string Identifier => "Restore Switch";

    public bool Front { get; } = front;

    public override string ToString() {
        return $"Restore {(Front ? "front" : "back")} switch";
    }

    public void Execute(BaseLocomotive locomotive) {
        var startLocation = SchedulerUtility.FirstCarLocation(locomotive, Front ? Car.End.F : Car.End.R);

        var firstSwitch = true;
        var items = new List<(TrackSegment Segment, TrackNode Node)>();
        foreach (var item in SchedulerUtility.GetRoute(startLocation)) {
            items.Add(item);
            if (!Graph.Shared.IsSwitch(item.Node)) {
                continue;
            }

            var distance = SchedulerUtility.Distance(startLocation, items);
            var (_, lastNode) = items.Last();
            var foulingDistance = Graph.Shared.CalculateFoulingDistance(lastNode);

            if (distance < foulingDistance && !firstSwitch) {
                break;
            }

            firstSwitch = false;
        }

        var (_, node) = items.Last();

        if (!SchedulerUtility.CanOperateSwitch(node, startLocation, locomotive)) {
            return;
        }

        SchedulerPlugin.DebugMessage($"NODE: {node.id}");
        if (SchedulerPlugin.Settings.SwitchStates.TryGetValue(node.id, out var state)) {
            node.isThrown = state;
        }
    }

    public IScheduleCommand Clone() {
        return new ScheduleCommandRestoreSwitch(Front);
    }
}

public sealed class ScheduleCommandRestoreSwitchSerializer : ScheduleCommandSerializerBase<ScheduleCommandRestoreSwitch>
{
    private bool? _Front;

    protected override void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer) {
        if (propertyName == "Front") {
            _Front = serializer.Deserialize<bool>(reader);
        }
    }

    protected override ScheduleCommandRestoreSwitch BuildScheduleCommand() {
        ThrowIfNull(_Front, "Front");

        return new ScheduleCommandRestoreSwitch(_Front!.Value);
    }


    public override void Write(JsonWriter writer, ScheduleCommandRestoreSwitch value) {
        writer.WritePropertyName("Front");
        writer.WriteValue(value.Front);
    }
}

public sealed class ScheduleCommandRestoreSwitchPanelBuilder : ScheduleCommandPanelBuilderBase
{
    private bool _Front;

    public override void BuildPanel(UIPanelBuilder builder) {
        builder.AddField("Location",
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Front of train", _Front, () => SetToggle(ref _Front, true));
                strip.AddButtonSelectable("Rear of train", !_Front, () => SetToggle(ref _Front, false));
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
        return new ScheduleCommandRestoreSwitch(_Front);
    }
}