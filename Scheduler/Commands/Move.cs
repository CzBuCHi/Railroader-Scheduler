using System.Collections;
using System.Collections.Generic;
using Game.Messages;
using Model;
using Model.AI;
using Newtonsoft.Json;
using Scheduler.Utility;
using Track;
using UI.Builder;
using UI.EngineControls;
using UnityEngine;

namespace Scheduler.Commands;

public class Move(bool forward, int? maxSpeed, int switchCount, bool before, bool clearSwitchesUnderTrain) : ICommand
{
    public string DisplayText =>
        $"Move {(Forward ? "forward" : "back")} at {(MaxSpeed == null ? "yard Speed" : $"max. speed {(MaxSpeed < 45 ? MaxSpeed + " MPH" : "")}")} and " +
        $"stop {(Before ? "before" : "after")} {GetOrdinal(SwitchCount)} switch{(ClearSwitchesUnderTrain ? " (from train end)" : "")}.";

    private static string GetOrdinal(int number) {
        return number % 100 is >= 11 and <= 13
            ? number + "th"
            : (number % 10) switch {
                1 => number + "st",
                2 => number + "nd",
                3 => number + "rd",
                _ => number + "th"
            };
    }

    public bool Forward { get; } = forward;
    public int? MaxSpeed { get; } = maxSpeed;
    public int SwitchCount { get; } = switchCount;
    public bool Before { get; } = before;
    public bool ClearSwitchesUnderTrain { get; } = clearSwitchesUnderTrain;
}

public class MoveManager : CommandManager<Move>
{
    public override IEnumerator Execute(Dictionary<string, object> state) {
        Location? startLocation = null;
        if (state.TryGetValue("startLocation", out var savedLocation)) {
            startLocation = (Location?)savedLocation;
            if (startLocation == Location.Invalid) {
                startLocation = null;
            }
        }

        Logger.Information("Move startLocation: " + startLocation);

        var locomotive = (BaseLocomotive)state["locomotive"]!;
        var persistence = new AutoEngineerPersistence(locomotive.KeyValueObject!);
        var distance = SchedulerUtility.GetDistanceForSwitchOrder(Command!.SwitchCount, Command.ClearSwitchesUnderTrain, Command.Before, locomotive,  Command.Forward, startLocation, out _, out var targetSegment);
        var helper = new AutoEngineerOrdersHelper(locomotive, persistence);

        helper.SetOrdersValue(Command.MaxSpeed == null ? AutoEngineerMode.Yard : AutoEngineerMode.Road, Command.Forward, Command.MaxSpeed, distance);

        // wait for AI to start moving ...
        yield return new WaitWhile(() => locomotive.IsStopped());
        
        var endLocation = Location.Invalid;

        // wait until AI stops ...
        yield return new WaitUntil(() => {
            if (!locomotive.IsStopped()) {
                return false;
            }
            
            // ... on correct track segment
            endLocation = SchedulerUtility.FirstCarLocation(locomotive, Command.Forward ? Car.End.R : Car.End.F);
            return endLocation.segment == targetSegment;
        });
        
        if (Command.MaxSpeed != null) {
            // put train to manual mode (otherwise UI will show Road mode, speed # when train is not moving)
            helper.SetOrdersValue(AutoEngineerMode.Off);
        }

        Logger.Information("Move endLocation: " + endLocation);
        state["startLocation"] = endLocation;
    }

    private bool? _Forward;
    private int _MaxSpeed;
    private int? _SwitchCount;
    private bool? _Before;
    private bool? _ClearSwitchesUnderTrain;
    private bool? _RoadMode;

    public override void SerializeProperties(JsonWriter writer) {
        writer.WritePropertyName(nameof(Move.Forward));
        writer.WriteValue(Command!.Forward);
        if (Command!.MaxSpeed != null) {
            writer.WritePropertyName(nameof(Move.MaxSpeed));
            writer.WriteValue(Command!.MaxSpeed.Value);
        }

        writer.WritePropertyName(nameof(Move.SwitchCount));
        writer.WriteValue(Command!.SwitchCount);
        writer.WritePropertyName(nameof(Move.Before));
        writer.WriteValue(Command!.Before);
        writer.WritePropertyName(nameof(Move.ClearSwitchesUnderTrain));
        writer.WriteValue(Command!.ClearSwitchesUnderTrain);
    }

    protected override void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer) {
        if (propertyName == nameof(Move.Forward)) {
            _Forward = serializer.Deserialize<bool>(reader);
        }

        if (propertyName == nameof(Move.MaxSpeed)) {
            _MaxSpeed = serializer.Deserialize<int>(reader);
        }

        if (propertyName == nameof(Move.SwitchCount)) {
            _SwitchCount = serializer.Deserialize<int>(reader);
        }

        if (propertyName == nameof(Move.Before)) {
            _Before = serializer.Deserialize<bool>(reader);
        }

        if (propertyName == nameof(Move.ClearSwitchesUnderTrain)) {
            _ClearSwitchesUnderTrain = serializer.Deserialize<bool>(reader);
        }
    }

    public override ICommand CreateCommand() {
        ThrowIfNull(_Forward, nameof(Move.Forward));
        ThrowIfNull(_SwitchCount, nameof(Move.SwitchCount));
        ThrowIfNull(_Before, nameof(Move.Before));
        ThrowIfNull(_ClearSwitchesUnderTrain, nameof(Move.ClearSwitchesUnderTrain));
        // MaxSpeed is optional
        var command = new Move(_Forward!.Value, _MaxSpeed, _SwitchCount!.Value, _Before!.Value, _ClearSwitchesUnderTrain!.Value);
        _Forward = null;
        return command;
    }

    private TrackNode? _TargetSwitch;     // target switch node
    private TrackSegment? _TargetSegment; // segment connected to target switch closer to train

    public override void BuildPanel(UIPanelBuilder builder, BaseLocomotive locomotive) {
        if (_Forward == null) {
            _Forward = true;
            _RoadMode = true;
            _MaxSpeed = 45;
            _ClearSwitchesUnderTrain = false;
            _SwitchCount = 1;
        }

        // ReSharper disable once StringLiteralTypo
        builder.AddField("Direction".Color("cccccc")!,
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Forward", _Forward == true, () => SetToggle(ref _Forward, true));
                strip.AddButtonSelectable("Backward", _Forward == false, () => SetToggle(ref _Forward, false));
            })!
        );
        builder.AddField("Mode",
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Road", _RoadMode == true, () => SetToggle(ref _RoadMode, true));
                strip.AddButtonSelectable("Yard", _RoadMode == false, () => SetToggle(ref _RoadMode, false));
            })!
        );
        if (_RoadMode == true) {
            builder.AddField("Max Speed",
                builder.AddSliderQuantized(() => _MaxSpeed,
                    () => _MaxSpeed.ToString("0"),
                    o => _MaxSpeed = (int)o, 5, 0, 45,
                    o => _MaxSpeed = (int)o
                )!
            );
        }

        builder.AddField("Stop Location",
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Before", _Before == true, () => SetToggle(ref _Before, true));
                strip.AddButtonSelectable("After", _Before == false, () => SetToggle(ref _Before, false));
            })!
        );
        builder.AddField("Switches",
            builder.ButtonStrip(strip => {
                    strip.AddField("Count", $"{_SwitchCount}");
                    if (_SwitchCount > 0) {
                        strip.AddButton("-1", () => {
                            --_SwitchCount;
                            strip.Rebuild();
                            MoveCameraToSwitch();
                        });
                    }

                    strip.AddButton("+1", () => {
                        _SwitchCount = (_SwitchCount ?? 0) + 1;
                        strip.Rebuild();
                        MoveCameraToSwitch();
                    });
                }
            )!
        );
        builder.AddField("Include under train", builder.AddToggle(() => _ClearSwitchesUnderTrain == true, o => _ClearSwitchesUnderTrain = o)!);

        return;

        void MoveCameraToSwitch() {
            Location? startLocation = null;
            // TODO: resolve start location

            SchedulerUtility.GetDistanceForSwitchOrder(_SwitchCount.Value, _ClearSwitchesUnderTrain!.Value, _Before!.Value, locomotive, _Forward.Value, startLocation, out _TargetSwitch, out _TargetSegment);
            if (_TargetSwitch != null) {
                SchedulerUtility.MoveCameraToNode(_TargetSwitch, true);
            }
        }

        void SetToggle(ref bool? field, bool value) {
            if (field == value) {
                return;
            }

            field = value;
            builder.Rebuild();
        }
    }
}
