#region

using Newtonsoft.Json;
using Scheduler.Commands.Abstract;
using Scheduler.Data;
using Track;
using UI.Builder;

#endregion

namespace Scheduler.Commands;

public sealed class ScheduleCommandRestoreSwitch(bool front) : ScheduleCommandSwitchBase(front)
{
    public override string Identifier => "Restore Switch";

    public override string ToString()
    {
        return $"Restore {(Front ? "front" : "back")} switch";
    }

    protected override void Execute(TrackNode node)
    {
        SchedulerPlugin.DebugMessage($"NODE: {node.id}");
        if (SchedulerPlugin.Settings.SwitchStates.TryGetValue(node.id, out var state))
        {
            node.isThrown = state;
        }
    }

    public override IScheduleCommand Clone()
    {
        return new ScheduleCommandRestoreSwitch(Front);
    }
}

public sealed class ScheduleCommandRestoreSwitchSerializer : ScheduleCommandSerializerBase<ScheduleCommandRestoreSwitch>
{
    private bool? _Front;

    protected override void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer)
    {
        if (propertyName == "Front")
        {
            _Front = serializer.Deserialize<bool>(reader);
        }
    }

    protected override ScheduleCommandRestoreSwitch BuildScheduleCommand()
    {
        ThrowIfNull(_Front, "Front");

        return new ScheduleCommandRestoreSwitch(_Front!.Value);
    }


    public override void Write(JsonWriter writer, ScheduleCommandRestoreSwitch value)
    {
        writer.WritePropertyName("Front");
        writer.WriteValue(value.Front);
    }
}

public sealed class ScheduleCommandRestoreSwitchPanelBuilder : ScheduleCommandPanelBuilderBase
{
    private bool _Front;

    public override void BuildPanel(UIPanelBuilder builder)
    {
        builder.AddField("Location",
            builder.ButtonStrip(strip =>
            {
                strip.AddButtonSelectable("Front of train", _Front, () => SetToggle(ref _Front, true));
                strip.AddButtonSelectable("Rear of train", !_Front, () => SetToggle(ref _Front, false));
            })!
        );

        return;

        void SetToggle(ref bool field, bool value)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            builder.Rebuild();
        }
    }

    public override IScheduleCommand CreateScheduleCommand()
    {
        return new ScheduleCommandRestoreSwitch(_Front);
    }
}