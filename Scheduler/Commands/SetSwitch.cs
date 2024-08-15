using System.Collections;
using System.Collections.Generic;
using Model;
using Newtonsoft.Json;
using Scheduler.Utility;
using Track;
using UI.Builder;

namespace Scheduler.Commands;

public sealed class SetSwitch(string id, bool isThrown) : ICommand
{
    public string DisplayText => $"Set switch '{Id}'";

    public string Id { get; } = id;
    public bool IsThrown { get; } = isThrown;
}

public sealed class SetSwitchManager : CommandManager<SetSwitch>
{
    public override IEnumerator Execute(Dictionary<string, object> state) {
        base.Execute(state);

        var locomotive = (BaseLocomotive)state["locomotive"]!;

        var node = Graph.Shared.GetNode(Command!.Id);
        if (node == null) {
            // TODO: Log error
            yield break;
        }

        if (!SchedulerUtility.CanOperateSwitch(node, locomotive)) {
            yield break;
        }

        if (!state.TryGetValue("switches", out var value) || value == null) {
            value = new Dictionary<string, bool>();
            state["switches"] = value;
        }

        var switches = (Dictionary<string, bool>)value;
        switches[node.id] = node.isThrown;

        node.isThrown = Command.IsThrown;
    }

    private string? _Id;
    private bool? _IsThrown;

    public override void Serialize(JsonWriter writer) {
        writer.WritePropertyName("Id");
        writer.WriteValue(Command!.Id);
    }

    protected override void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer) {
        if (propertyName == "Id") {
            _Id = serializer.Deserialize<string>(reader);
        }

        if (propertyName == "IsThrown") {
            _IsThrown = serializer.Deserialize<bool>(reader);
        }
    }

    public override ICommand CreateCommand() {
        ThrowIfNull(_Id, "Id");
        ThrowIfNull(_IsThrown, "IsThrown");
        return new SetSwitch(_Id!, _IsThrown!.Value);
    }

    private TrackNode? _Node;       // current switch node
    private TrackSegment? _Segment; // segment connected to _Node closer to train

    public override void BuildPanel(UIPanelBuilder builder, BaseLocomotive locomotive) {
        if (_Node == null) {
            if (!SchedulerUtility.FindSwitchNearTrain(locomotive, out _Segment, out _Node)) {
                // TODO: Log error
                return;
            }

            _Id = _Node.id;
        }

        builder.AddField("Orientation",
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Normal", _IsThrown == false, () => SetToggle(ref _IsThrown, false));
                strip.AddButtonSelectable("Reversed", _IsThrown == true, () => SetToggle(ref _IsThrown, true));
            })!
        );
        builder.AddField("Switch",
            builder.ButtonStrip(strip => {
                strip.AddButton("Previous", () => {
                    if (SchedulerUtility.GetPreviousSegmentOrRoute(ref _Segment!, ref _Node)) {
                        _Id = _Node.id;
                        SchedulerUtility.MoveCameraToNode(_Node, false);
                        builder.Rebuild();
                    }
                });
                strip.AddButton("Next", () => {
                    if (SchedulerUtility.GetNextSegmentOnRoute(ref _Segment!, ref _Node)) {
                        _Id = _Node.id;
                        SchedulerUtility.MoveCameraToNode(_Node, false);
                        builder.Rebuild();
                    }
                });
            })!
        );

        return;

        void SetToggle(ref bool? field, bool value) {
            if (field == value) {
                return;
            }

            field = value;
            builder.Rebuild();
        }
    }
}
