using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight.Messaging;
using Game.Messages;
using Model;
using Model.AI;
using Newtonsoft.Json;
using Scheduler.Extensions;
using Scheduler.Messages;
using Scheduler.Utility;
using Scheduler.Visualizers;
using Serilog;
using Track;
using UI.Builder;
using UI.EngineControls;
using UnityEngine;
using ILogger = Serilog.ILogger;
using Location = Track.Location;

namespace Scheduler.Commands;

public sealed class Move(string id, StopMode stopMode, bool forward, int carIndex, int? maxSpeed) : ICommand
{
    public string DisplayText {
        get {
            var sb = new StringBuilder();
            sb.Append("Move ").Append(Forward ? "forward" : "backward")
              .Append(" at ").Append(MaxSpeed == null ? "yard speed" : $"max speed {MaxSpeed} MPH")
              .Append("\r\n  stop ");

            switch (StopMode) {
                case StopMode.BeforeSwitch:
                case StopMode.AfterSwitch:
                    sb.Append(Math.Abs(CarIndex).GetRelativePosition()).Append(StopMode == StopMode.BeforeSwitch ? "before" : "after")
                      .Append(" switch '").Append(Id).Append("'");
                    break;
                case StopMode.CarLengths:
                    sb.Append("after ").Append(CarIndex).Append(" car lengths");
                    break;
                case StopMode.EndOfTrack:
                    sb.Append("at end of track");
                    break;
            }

            return sb.ToString();
        }
    }

    public string Id { get; } = id;
    public StopMode StopMode { get; } = stopMode;
    public bool Forward { get; } = forward;
    public int CarIndex { get; } = carIndex;
    public int? MaxSpeed { get; } = maxSpeed;
}

public enum StopMode
{
    BeforeSwitch,
    AfterSwitch,
    EndOfTrack,
    CarLengths,
}

public sealed class MoveManager : CommandManager<Move>, IDisposable
{
    private static readonly ILogger _Logger = Log.ForContext(typeof(SchedulerUtility))!;

    public MoveManager() {
        Messenger.Default!.Register<SelectedSwitchChanged>(this, OnSelectedSwitchChanged);
    }

    public void Dispose() {
        Messenger.Default!.Unregister(this);
    }

    private void OnSelectedSwitchChanged(SelectedSwitchChanged _) {
        _Id = SchedulerPlugin.SelectedSwitch?.id;
    }

    public override bool ShowTrackSwitchVisualizers => true;

    private SchedulerUtility.FinalRoute? GetTargetLocation(Location location) {
        switch (Command!.StopMode) {
            case StopMode.BeforeSwitch:
            case StopMode.AfterSwitch:
                var node = Graph.Shared.GetNode(Command!.Id);
                if (node == null) {
                    _Logger.Information("  node not found");
                    return null;
                }

                _Logger.Information($"  to {node}");
                if (SchedulerPlugin.Settings.Debug) {
                    TrackNodeVisualizer.Shared.Show(node);
                }

                return SchedulerUtility.GetDistanceToSwitch(location, node);

            case StopMode.EndOfTrack:
                return SchedulerUtility.GetDistanceToTrackEnd(location);

            case StopMode.CarLengths:
                var distance = Command.CarIndex * 12.2f;
                var targetLocation = Graph.Shared.LocationByMoving(location, distance);
                return new SchedulerUtility.FinalRoute(distance, targetLocation);

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public override IEnumerator Execute(Dictionary<string, object> state) {
        var locomotive = (BaseLocomotive)state["locomotive"]!;

        _Logger.Information($"Move {locomotive}");

        var consist = locomotive.EnumerateConsist();
        var cars = consist.ToDictionary(o => o.Position, o => o.Car);
        if (!cars.TryGetValue(Command!.CarIndex, out var car)) {
            yield break;
        }

        car!.SetHandbrake(true);
        yield return new WaitForSecondsRealtime(1f);
        car!.SetHandbrake(false);

        var carEnd = Command.Forward ? Car.End.R : Car.End.F;

        _Logger.Information($"  car {car}");

        var location = Command.Forward ? car.LocationR.Flipped() : car.LocationF;
        _Logger.Information($"  car location {location}");

        _Logger.Information($"  location {location}");
        if (SchedulerPlugin.Settings.Debug) {
            LocationVisualizer.Shared.Show(location, Color.green);
        }

        var route = GetTargetLocation(location);
        if (route == null) {
            _Logger.Information("  route not found");
            yield break;
        }

        _Logger.Information($"  distance {route.Distance}");

        state["wage"] = (int)state["wage"] + (int)Math.Round(route.Distance * 0.01);

        var endLocation = route.Location;
        
        var persistence = new AutoEngineerPersistence(locomotive.KeyValueObject!);
        var helper = new AutoEngineerOrdersHelper(locomotive, persistence);
        helper.SetOrdersValue(Command.MaxSpeed == null ? AutoEngineerMode.Yard : AutoEngineerMode.Road, Command.Forward, Command.MaxSpeed, route.Distance);


        _Logger.Information("  waiting for AI to start moving ...");

        // wait for AI to start moving ...
        yield return new WaitWhile(() => locomotive.IsStopped());

        yield return new WaitForSecondsRealtime(0.5f);

        _Logger.Information($"  target car {car}");
        _Logger.Information("  waiting for AI to stop moving ...");

        var stopMove = false;
        var observer = persistence.ObserveOrders(_ => stopMove = true, false);

        // wait until AI stops ...
        yield return new WaitUntil(() => {
            if (stopMove) {
                _Logger.Information("  stopMove");
                return true;
            }

            if (!locomotive.IsStopped()) {
                return false;
            }

            // ... on correct track segment
            var carLocation = SchedulerUtility.GetCarLocation(car, carEnd);
            if (SchedulerPlugin.Settings.Debug) {
                LocationVisualizer.Shared.Show(carLocation, Color.red);
            }

            _Logger.Information($"  checking if {carLocation.segment.id} is {endLocation.segment.id}");
            return endLocation.segment == carLocation.segment;
        });

        observer.Dispose();

        if (stopMove) {
            state["stop"] = true;
            yield break;
        }

        if (Command.MaxSpeed != null) {
            // put train to manual mode (otherwise UI will show Road mode, when train is not moving)
            helper.SetOrdersValue(AutoEngineerMode.Off);
        }

        _Logger.Information("  move completed ...");
    }

    private string? _Id;
    private StopMode? _StopMode;
    private bool? _Forward;
    private int? _CarIndex;
    private int? _MaxSpeed;
    private bool _RoadMode;
    
    public override void SerializeProperties(JsonWriter writer) {
        writer.WritePropertyName(nameof(Move.Id));
        writer.WriteValue(Command!.Id);

        writer.WritePropertyName(nameof(Move.StopMode));
        writer.WriteValue((int)Command!.StopMode);

        writer.WritePropertyName(nameof(Move.Forward));
        writer.WriteValue(Command!.Forward);

        writer.WritePropertyName(nameof(Move.CarIndex));
        writer.WriteValue(Command!.CarIndex);
        
        if (Command!.MaxSpeed != null) {
            writer.WritePropertyName(nameof(Move.MaxSpeed));
            writer.WriteValue(Command!.MaxSpeed.Value);
        }
    }

    protected override void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer) {
        if (propertyName == nameof(Move.Id)) {
            _Id = serializer.Deserialize<string>(reader);
        }

        if (propertyName == nameof(Move.StopMode)) {
            _StopMode = (StopMode?)serializer.Deserialize<int>(reader);
        }

        if (propertyName == nameof(Move.Forward)) {
            _Forward = serializer.Deserialize<bool>(reader);
        }

        if (propertyName == nameof(Move.CarIndex)) {
            _CarIndex = serializer.Deserialize<int>(reader);
        }

        if (propertyName == nameof(Move.MaxSpeed)) {
            _MaxSpeed = serializer.Deserialize<int>(reader);
        }
    }

    protected override object TryCreateCommand() {
        List<string> missing = new();
        if (_Id == null) {
            missing.Add("Id");
        }

        if (_StopMode == null) {
            missing.Add("StopMode");
        }

        if (_Forward == null) {
            missing.Add("Forward");
        }

        if (_CarIndex == null) {
            missing.Add("CarIndex");
        }

        if (missing.Count > 0) {
            return $"Missing mandatory property '{string.Join(", ", missing)}'.";
        }

        return new Move(_Id!, _StopMode!.Value, _Forward!.Value, _CarIndex!.Value, _RoadMode ? _MaxSpeed : null);
    }

    public override void BuildPanel(UIPanelBuilder builder, BaseLocomotive locomotive) {
        if (_Forward == null) {
            _Forward = true;
            _RoadMode = true;
            _MaxSpeed = 45;
        }

        builder.RebuildOnEvent<SelectedSwitchChanged>();

        // ReSharper disable once StringLiteralTypo
        builder.AddField("Direction".Color("cccccc"), 
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Forward", _Forward == true, () => SetDirection(true));
                strip.AddButtonSelectable("Backward", _Forward == false, () => SetDirection(false));
            })!
        );

        builder.AddField("Mode".Color("cccccc"),
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

        builder.AddField("Switch", builder.AddInputField(_Id ?? "", o => _Id = o, "You can select Id by clicking on switch")!);

        builder.AddField("Stop location", 
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Before switch", _StopMode == StopMode.BeforeSwitch, () => SetStopMode(StopMode.BeforeSwitch));
                strip.AddButtonSelectable("After switch", _StopMode == StopMode.AfterSwitch, () => SetStopMode(StopMode.AfterSwitch));
                strip.AddButtonSelectable("End of track", _StopMode == StopMode.EndOfTrack, () => SetStopMode(StopMode.EndOfTrack));
                strip.AddButtonSelectable("Car lengths", _StopMode == StopMode.CarLengths, () => SetStopMode(StopMode.CarLengths));
            })!
        );

        switch (_StopMode) {
            case StopMode.BeforeSwitch:
            case StopMode.AfterSwitch:
                var consist = locomotive.EnumerateConsist();
                var carIndices = consist.ToArray();

                var trainCars = carIndices
                                .Select(o => o.Position == 0
                                    ? $"Locomotive ({o.Car!.DisplayName})"
                                    : $"{o.Position.GetRelativePosition()} ({o.Car!.DisplayName})")
                                .ToList();

                var trainCarsPositions = carIndices.Select(o => o.Position).ToList();
                var index = Array.FindIndex(carIndices, o => o.Position == _CarIndex);

                builder.AddField("Target car",
                    builder.ButtonStrip(strip => {
                        strip.AddButtonSelectable("First", index == 0, () => SetCarIndex(0));
                        strip.AddButtonSelectable("Last", index == trainCars.Count - 1, () => SetCarIndex(trainCars.Count - 1));
                    })!
                );

                builder.AddField("Car index", builder.AddDropdown(trainCars, index, SetCarIndex)!);

                break;

                void SetCarIndex(int dropdownIndex) {
                    var newCarIndex = trainCarsPositions[dropdownIndex];
                    if (_CarIndex == newCarIndex) {
                        return;
                    }

                    _CarIndex = newCarIndex;
                    builder.Rebuild();
                }

            case StopMode.CarLengths:
                builder.AddField("Car Lengths".Color("cccccc"),
                    builder.ButtonStrip(strip => {
                        strip.AddButtonSelectable("1", _CarIndex == 1, () => SetCarLengths(1));
                        strip.AddButtonSelectable("2", _CarIndex == 2, () => SetCarLengths(2));
                        strip.AddButtonSelectable("5", _CarIndex == 5, () => SetCarLengths(5));
                        strip.AddButtonSelectable("10", _CarIndex == 10, () => SetCarLengths(10));
                        strip.AddButtonSelectable("20", _CarIndex == 20, () => SetCarLengths(20));
                    })!
                );

                break;
        }

        return;

        void SetCarLengths(int carLengths) {
            if (_CarIndex == carLengths) {
                return;
            }

            _CarIndex = carLengths;
            builder.Rebuild();
        }

        void SetDirection(bool forward) {
            if (_Forward == forward) {
                return;
            }

            _Forward = forward;
            builder.Rebuild();
        }

        void SetRoadMode(bool roadMode) {
            if (_RoadMode == roadMode) {
                return;
            }

            _RoadMode = roadMode;
            builder.Rebuild();
        }

        void SetStopMode(StopMode stopMode) {
            if (_StopMode == stopMode) {
                return;
            }

            _StopMode = stopMode;
            builder.Rebuild();
        }
    }
}
