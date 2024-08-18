using System;
using System.Collections;
using System.Collections.Generic;
using GalaSoft.MvvmLight.Messaging;
using Game.Messages;
using Model;
using Model.AI;
using Newtonsoft.Json;
using Scheduler.Messages;
using Scheduler.Utility;
using Track;
using UI.Builder;
using UI.EngineControls;
using UnityEngine;

namespace Scheduler.Commands;

public class Move(bool forward, int? maxSpeed, string? switchId, bool? stopBefore, int? carLength) : ICommand
{
    public string DisplayText =>
        $"Move {(Forward ? "forward" : "back")} at {(MaxSpeed == null ? "yard Speed" : $"max. speed {(MaxSpeed < 45 ? MaxSpeed + " MPH" : "")}")} and " +
        $"stop {(CarLength != null ? $"after {CarLength} car lengths." : $"{(StopBefore == true ? "before" : "after")} switch '{SwitchId}'.")}";

    public bool Forward { get; } = forward;
    public int? MaxSpeed { get; } = maxSpeed;
    public string? SwitchId { get; } = switchId;
    public bool? StopBefore { get; } = stopBefore;
    public int? CarLength { get; } = carLength;
}

public enum MoveStopLocation
{
    BeforeSwitch,
    AfterSwitch,
    CarLengths
}

public class MoveManager : CommandManager<Move>, IDisposable
{
    public MoveManager() {
        Messenger.Default!.Register<SelectedSwitchChanged>(this, OnSelectedSwitchChanged);
    }

    public void Dispose() {
        Messenger.Default!.Unregister(this);
    }

    private void OnSelectedSwitchChanged(SelectedSwitchChanged _) {
        _SwitchId = SchedulerPlugin.SelectedSwitch?.id;
    }

    public override IEnumerator Execute(Dictionary<string, object> state) {
        var locomotive = (BaseLocomotive)state["locomotive"]!;

        Location startLocation;
        if (state.TryGetValue("startLocation", out var savedLocation)) {
            startLocation = (Location)savedLocation;
        } else {
            startLocation = SchedulerUtility.FirstCarLocation(locomotive, Car.End.F);
        }

        Logger.Information("Move startLocation: " + startLocation);

        var persistence = new AutoEngineerPersistence(locomotive.KeyValueObject!);

        float distance = 0;
        if (Command!.CarLength != null) {
            distance = Command.CarLength.Value * 12.2f;
            Graph.Shared.LocationByMoving(startLocation, distance, true);
        }


        // todo: distance between train & target switch
        // string? SwitchId 
        // bool? StopBefore 
        // TODO //SchedulerUtility.GetDistanceForSwitchOrder(Command!.SwitchCount, Command.ClearSwitchesUnderTrain, Command.Before, locomotive,  Command.Forward, startLocation, out _, out var targetSegment);
        TrackSegment? targetSegment = null;

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
    private int? _MaxSpeed;
    private string? _SwitchId;
    private bool? _StopBefore;
    private int? _CarIndex;
    private int? _CarLength;

    private bool _RoadMode;
    private MoveStopLocation? _StopLocation;

    private int _DropdownIndex;

    public override void SerializeProperties(JsonWriter writer) {
        writer.WritePropertyName(nameof(Move.Forward));
        writer.WriteValue(Command!.Forward);

        if (Command!.MaxSpeed != null) {
            writer.WritePropertyName(nameof(Move.MaxSpeed));
            writer.WriteValue(Command!.MaxSpeed.Value);
        }

        if (Command!.SwitchId != null) {
            writer.WritePropertyName(nameof(Move.SwitchId));
            writer.WriteValue(Command!.SwitchId);
        }

        writer.WritePropertyName(nameof(Move.StopBefore));
        writer.WriteValue(Command!.StopBefore);

        writer.WritePropertyName(nameof(Move.CarLength));
        writer.WriteValue(Command!.CarLength);
    }

    protected override void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer) {
        if (propertyName == nameof(Move.Forward)) {
            _Forward = serializer.Deserialize<bool>(reader);
        }

        if (propertyName == nameof(Move.MaxSpeed)) {
            _MaxSpeed = serializer.Deserialize<int>(reader);
        }

        if (propertyName == nameof(Move.SwitchId)) {
            _SwitchId = serializer.Deserialize<string>(reader);
        }

        if (propertyName == nameof(Move.StopBefore)) {
            _StopBefore = serializer.Deserialize<bool>(reader);
        }

        if (propertyName == nameof(Move.CarLength)) {
            _CarLength = serializer.Deserialize<int>(reader);
        }
    }

    public override ICommand CreateCommand() {
        ThrowIfNull(_Forward, nameof(Move.Forward));

        if (_SwitchId == null && _CarLength == null) {
            throw new JsonSerializationException("Missing mandatory property 'SwitchId' or 'CarLength'.");
        }

        var command = new Move(_Forward!.Value, _RoadMode ? _MaxSpeed : null, _SwitchId, _StopBefore, _CarLength);

        Logger.Information("CreateCommand: " + JsonConvert.SerializeObject(command));

        _Forward = null;
        return command;
    }

    public override bool ShowTrackSwitchVisualizers => _StopLocation != MoveStopLocation.CarLengths;

    public override void BuildPanel(UIPanelBuilder builder, BaseLocomotive locomotive) {
        if (_Forward == null) {
            _Forward = true;
            _RoadMode = true;
            _MaxSpeed = 45;
            _SwitchId = null;
            _StopBefore = false;
            _CarLength = null;
        }

        // ReSharper disable once StringLiteralTypo
        builder.AddField("Direction".Color("cccccc")!,
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Forward", _Forward == true, () => SetForward(true));
                strip.AddButtonSelectable("Backward", _Forward == false, () => SetForward(false));
            })!
        );

        builder.AddField("Mode",
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Road", _RoadMode, () => SetRoadMode(true));
                strip.AddButtonSelectable("Yard", !_RoadMode, () => SetRoadMode(false));
            })!
        );

        if (_RoadMode) {
            builder.AddField("Max Speed",
                builder.AddSliderQuantized(() => _MaxSpeed ?? 0,
                    () => (_MaxSpeed ?? 0).ToString("0"),
                    o => _MaxSpeed = (int)o, 5, 0, 45,
                    o => _MaxSpeed = (int)o
                )!
            );
        }

        builder.AddField("Stop Location",
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Before switch", _StopLocation == MoveStopLocation.BeforeSwitch, () => SetStopLocation(MoveStopLocation.BeforeSwitch));
                strip.AddButtonSelectable("After switch", _StopLocation == MoveStopLocation.AfterSwitch, () => SetStopLocation(MoveStopLocation.AfterSwitch));
                strip.AddButtonSelectable("Car lengths", _StopLocation == MoveStopLocation.CarLengths, () => SetStopLocation(MoveStopLocation.CarLengths));
            })!
        );

        switch (_StopLocation) {
            case MoveStopLocation.BeforeSwitch:
            case MoveStopLocation.AfterSwitch:
                builder.AddField("Switch", builder.AddInputField(_SwitchId ?? "", o => _SwitchId = o, "You can select Id by clicking on switch")!);

                SchedulerUtility.ResolveTrainCars(locomotive, out var trainCars, out var trainCarsPositions, false);
                builder.AddField("Car index",
                    builder.AddDropdown(trainCars, _DropdownIndex, o => {
                        _DropdownIndex = o;
                        _CarIndex = trainCarsPositions[o];
                        builder.Rebuild();
                    })!
                );
                break;

            case MoveStopLocation.CarLengths:
                builder.AddField("Car lengths",
                    builder.ButtonStrip(strip => {
                        strip.AddButtonSelectable("1", _CarLength == 1, () => SetCarLength(1));
                        strip.AddButtonSelectable("2", _CarLength == 2, () => SetCarLength(2));
                        strip.AddButtonSelectable("5", _CarLength == 5, () => SetCarLength(5));
                        strip.AddButtonSelectable("10", _CarLength == 10, () => SetCarLength(10));
                        strip.AddButtonSelectable("20", _CarLength == 20, () => SetCarLength(20));
                    })!
                );
                break;
        }

        return;

        void SetStopLocation(MoveStopLocation value) {
            if (_StopLocation == value) {
                return;
            }

            _StopLocation = value;
            builder.Rebuild();
        }

        void SetCarLength(int value) {
            if (_CarLength == value) {
                return;
            }

            _CarLength = value;
            builder.Rebuild();
        }

        void SetForward(bool value) {
            if (_Forward == value) {
                return;
            }

            _Forward = value;
            builder.Rebuild();
        }

        void SetRoadMode(bool value) {
            if (_RoadMode == value) {
                return;
            }

            _RoadMode = value;
            builder.Rebuild();
        }
    }
}
