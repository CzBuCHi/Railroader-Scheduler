using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

namespace Scheduler.Commands;

public sealed class Move(string id, bool stopBefore, bool forward, int carIndex, int? maxSpeed) : ICommand
{
    public string DisplayText =>
        $"Move {(Forward ? "forward" : "backward")} at {(MaxSpeed == null ? "yard speed" : $"max speed {MaxSpeed} MPH")}\r\n  " +
        $"stop {Math.Abs(CarIndex).GetRelativePosition()} {(StopBefore ? "before" : "after")} switch '{Id}'.";
    
    public int Wage { get; } = 50;

    public string Id { get; } = id;
    public bool StopBefore { get; } = stopBefore;
    public bool Forward { get; } = forward;
    public int CarIndex { get; } = carIndex;
    public int? MaxSpeed { get; } = maxSpeed;
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

    protected override IEnumerator ExecuteCore(Dictionary<string, object> state) {
        var locomotive = (BaseLocomotive)state["locomotive"]!;

        _Logger.Information($"Move {locomotive}");

        var node = Graph.Shared.GetNode(Command!.Id);
        if (node == null) {
            _Logger.Information("  node not found");
            yield break;
        }

        _Logger.Information($"  to {node}");
        if (SchedulerPlugin.Settings.Debug) {
            TrackNodeVisualizer.Shared.Show(node);
        }

        var car = locomotive.EnumerateConsist().Where(o => o.Position == Command.CarIndex).Select(o => o.Car!).First();
        var carEnd = Command.Forward ? Car.End.R : Car.End.F;

        _Logger.Information($"  car {car}");

        var location = Location.Invalid;
        if (state.TryGetValue("location", out var locationOverride)) {
            location = (Location)locationOverride;
            _Logger.Information($"  saved location {location}");
        }

        if (!location.IsValid) {
            location = Command.Forward ? car.LocationR : car.LocationF;
            _Logger.Information($"  car location {location}");
        }

        _Logger.Information($"  location {location}");
        if (SchedulerPlugin.Settings.Debug) {
            LocationVisualizer.Shared.Show(location);
        }

        var route = SchedulerUtility.GetDistanceToSwitch(location, node);
        if (route == null) {
            _Logger.Information("  route not found");
            yield break;
        }

        _Logger.Information($"  distance {route.Distance}");
        _Logger.Information($"  nodes {string.Join(",", route.Nodes.Select(o => o.ToString()))}");

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

        bool statusChanged = false;
        // wait until AI stops ...
        yield return new WaitUntil(() => {
            persistence.ObservePlannerStatusChanged(() => statusChanged = true);
            _Logger.Information("  statusChanged: " + statusChanged);
            if (statusChanged) {
                return true;
            }

            if (!locomotive.IsStopped()) {
                return false;
            }

            // ... on correct track segment
            var carLocation = SchedulerUtility.GetCarLocation(car, carEnd);

            _Logger.Information($"  checking if {carLocation.segment.id} is {endLocation.segment.id}");
            return endLocation.segment == carLocation.segment;
        });

        if (Command.MaxSpeed != null) {
            // put train to manual mode (otherwise UI will show Road mode, when train is not moving)
            helper.SetOrdersValue(AutoEngineerMode.Off);
        }
        _Logger.Information("  move completed ...");
        state["location"] = endLocation;
    }

    private string? _Id;
    private bool? _StopBefore;
    private bool? _Forward;
    private int? _CarIndex;
    private int? _MaxSpeed;
    private bool _RoadMode;

    public override void SerializeProperties(JsonWriter writer) {
        writer.WritePropertyName(nameof(Move.Id));
        writer.WriteValue(Command!.Id);

        writer.WritePropertyName(nameof(Move.StopBefore));
        writer.WriteValue(Command!.StopBefore);

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

        if (propertyName == nameof(Move.StopBefore)) {
            _StopBefore = serializer.Deserialize<bool>(reader);
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

        if (_StopBefore == null) {
            missing.Add("StopBefore");
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

        return new Move(_Id!, _StopBefore!.Value, _Forward!.Value, _CarIndex!.Value, _RoadMode ? _MaxSpeed : null);
    }

    public override void BuildPanel(UIPanelBuilder builder, BaseLocomotive locomotive) {
        if (_Forward == null) {
            _Forward = true;
            _RoadMode = true;
            _MaxSpeed = 45;
        }

        builder.RebuildOnEvent<SelectedSwitchChanged>();

        // ReSharper disable once StringLiteralTypo
        builder.AddField("Direction".Color("cccccc")!, 
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Forward", _Forward == true, () => SetDirection(true));
                strip.AddButtonSelectable("Backward", _Forward == false, () => SetDirection(false));
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

        builder.AddField("Switch", builder.AddInputField(_Id ?? "", o => _Id = o, "You can select Id by clicking on switch")!);

        builder.AddField("Stop location", 
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Before switch", _StopBefore == true, () => SetStopBefore(true));
                strip.AddButtonSelectable("After switch", _StopBefore == false, () => SetStopBefore(false));
            })!
        );

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

        return;

        void SetCarIndex(int dropdownIndex) {
            var newCarIndex = trainCarsPositions[dropdownIndex];
            if (_CarIndex == newCarIndex) {
                return;
            }

            _CarIndex = newCarIndex;
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

        void SetStopBefore(bool stopBefore) {
            if (_StopBefore == stopBefore) {
                return;
            }

            _StopBefore = stopBefore;
            builder.Rebuild();
        }
    }
}
