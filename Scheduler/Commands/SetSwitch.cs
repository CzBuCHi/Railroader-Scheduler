using System;
using System.Collections;
using System.Collections.Generic;
using GalaSoft.MvvmLight.Messaging;
using Model;
using Newtonsoft.Json;
using Scheduler.Messages;
using Scheduler.Utility;
using Track;
using UI.Builder;

namespace Scheduler.Commands;

/// <summary> Save current switch state and set desired state. </summary>
/// <param name="id">Target switch id.</param>
/// <param name="isThrown">Desired state of switch.</param>
public sealed class SetSwitch(string id, bool isThrown) : ICommand
{
    public string DisplayText => $"Set switch '{Id}' to {(IsThrown ? "reverse" : "normal")}";
    public int Wage { get; } = 5;

    public string Id { get; } = id;
    public bool IsThrown { get; } = isThrown;
}

public sealed class SetSwitchManager : CommandManager<SetSwitch>, IDisposable
{
    public SetSwitchManager() {
        Messenger.Default!.Register<SelectedSwitchChanged>(this, OnSelectedSwitchChanged);
    }

    public void Dispose() {
        Messenger.Default!.Unregister(this);
    }

    private void OnSelectedSwitchChanged(SelectedSwitchChanged _) {
        _Id = SchedulerPlugin.SelectedSwitch?.id;
    }

    public override bool ShowTrackSwitchVisualizers => true;

    protected override IEnumerator ExecuteCore(Dictionary<string, object> state) {
        var locomotive = (BaseLocomotive)state["locomotive"]!;

        var node = Graph.Shared.GetNode(Command!.Id);
        if (node == null) {
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

    public override void SerializeProperties(JsonWriter writer) {
        writer.WritePropertyName(nameof(SetSwitch.Id));
        writer.WriteValue(Command!.Id);

        writer.WritePropertyName(nameof(SetSwitch.IsThrown));
        writer.WriteValue(Command!.IsThrown);
    }

    protected override void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer) {
        if (propertyName == nameof(SetSwitch.Id)) {
            _Id = serializer.Deserialize<string>(reader);
        }

        if (propertyName == nameof(SetSwitch.IsThrown)) {
            _IsThrown = serializer.Deserialize<bool>(reader);
        }
    }

    protected override object TryCreateCommand() {
        List<string> missing = new();
        if (_Id == null) {
            missing.Add("Id");
        }

        if (_IsThrown == null) {
            missing.Add("IsThrown");
        }

        if (missing.Count > 0) {
            return $"Missing mandatory property '{string.Join(", ", missing)}'.";
        }

        return new SetSwitch(_Id!, _IsThrown!.Value);
    }

    public override void BuildPanel(UIPanelBuilder builder, BaseLocomotive locomotive) {
        builder.RebuildOnEvent<SelectedSwitchChanged>();
        builder.AddField("Switch", builder.AddInputField(_Id ?? "", o => _Id = o, "You can select Id by clicking on switch")!);
        builder.AddField("Reversed", builder.AddToggle(() => _IsThrown == true, o => SetToggle(ref _IsThrown, o))!);
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
