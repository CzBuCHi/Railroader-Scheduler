using System;
using Newtonsoft.Json;
using Scheduler.Commands.OLD.Abstract;
using Scheduler.Data;
using Track;
using UI.Builder;

namespace Scheduler.Commands.OLD;
[Obsolete]
public sealed class ScheduleCommandSetSwitch(bool front, bool isThrown) : ScheduleCommandSwitchBase(front)
{
    public override string Identifier => "Set Switch";

    public bool IsThrown { get; } = isThrown;

    public override string ToString()
    {
        return $"Set {(Front ? "front" : "back")} switch to {(IsThrown ? "Reverse" : "Normal")}";
    }

    protected override void Execute(TrackNode node)
    {
        SchedulerPlugin.DebugMessage($"NODE: {node.id}");
        SchedulerPlugin.Settings.SwitchStates[node.id] = node.isThrown;
        node.isThrown = IsThrown;
    }

    public override IScheduleCommand Clone()
    {
        return new ScheduleCommandSetSwitch(Front, IsThrown);
    }
}
[Obsolete]
public sealed class ScheduleCommandSetSwitchSerializer : ScheduleCommandSerializerBase<ScheduleCommandSetSwitch>
{
    private bool? _Front;
    private bool? _IsThrown;

    protected override void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer)
    {
        switch (propertyName)
        {
            case "Front":
                _Front = serializer.Deserialize<bool>(reader);
                break;

            case "IsThrown":
                _IsThrown = serializer.Deserialize<bool>(reader);
                break;
        }
    }

    protected override ScheduleCommandSetSwitch BuildScheduleCommand()
    {
        ThrowIfNull(_Front, "Front");
        ThrowIfNull(_IsThrown, "IsThrown");

        return new ScheduleCommandSetSwitch(_Front!.Value, _IsThrown!.Value);
    }


    public override void Write(JsonWriter writer, ScheduleCommandSetSwitch value)
    {
        writer.WritePropertyName("Front");
        writer.WriteValue(value.Front);
        writer.WritePropertyName("IsThrown");
        writer.WriteValue(value.IsThrown);
    }
}
[Obsolete]
public sealed class ScheduleCommandSetSwitchPanelBuilder : ScheduleCommandPanelBuilderBase
{
    private bool _Front;
    private bool _IsThrown;

    public override void BuildPanel(UIPanelBuilder builder)
    {
        builder.AddField("Location",
            builder.ButtonStrip(strip =>
            {
                strip.AddButtonSelectable("Front of train", _Front, () => SetToggle(ref _Front, true));
                strip.AddButtonSelectable("Rear of train", !_Front, () => SetToggle(ref _Front, false));
            })!
        );
        builder.AddField("Orientation",
            builder.ButtonStrip(strip =>
            {
                strip.AddButtonSelectable("Normal", !_IsThrown, () => SetToggle(ref _IsThrown, false));
                strip.AddButtonSelectable("Reversed", _IsThrown, () => SetToggle(ref _IsThrown, true));
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
        return new ScheduleCommandSetSwitch(_Front, _IsThrown);
    }
}
