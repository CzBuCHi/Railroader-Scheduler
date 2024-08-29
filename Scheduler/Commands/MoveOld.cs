/*using System;
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

public sealed class MoveManagerOld : CommandManager<MoveOld>, IDisposable
{
    private static readonly ILogger _Logger = Log.ForContext(typeof(SchedulerUtility))!;

    public MoveManagerOld() {
        Messenger.Default!.Register<SelectedSwitchChanged>(this, OnSelectedSwitchChanged);
    }

    public void Dispose() {
        Messenger.Default!.Unregister(this);
    }

    private void OnSelectedSwitchChanged(SelectedSwitchChanged _) {
        _Id = SchedulerPlugin.SelectedSwitch?.id;
    }

    public override bool ShowTrackSwitchVisualizers => true;

    
    public override IEnumerator Execute(Dictionary<string, object> state) {
        var locomotive = (BaseLocomotive)state["locomotive"]!;

        _Logger.Information($"MoveOld {locomotive}");

        var carEnd = Command!.Forward ? Car.End.R : Car.End.F;
        Car car;
        if (Command!.StopMode == StopMode.CarLengths) {
            car = locomotive.EnumerateCoupled(carEnd)!.First();
        } else {
            var consist = locomotive.EnumerateConsist();
            var cars = consist.ToDictionary(o => o.Position, o => o.Car);
            if (!cars.TryGetValue(Command!.CarIndex, out car)) {
                yield break;
            }
            
            _Logger.Information($"  car {car}");
        }

        var location = Command.Forward ? car.LocationR.Flipped() : car.LocationF;
        _Logger.Information($"  car location {location}");

        if (SchedulerPlugin.Settings.Debug) {
            LocationVisualizer.Shared.Show(location, Color.green);
        }

        var route = GetTargetLocation(location);
        if (route == null) {
            _Logger.Information("  route not found");
            yield break;
        }

        _Logger.Information($"  distance {route.Distance}");

        if (route.Distance > 100) {
            state["wage"] = (int)state["wage"] + 10;
        }

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



    public override void BuildPanel(UIPanelBuilder builder, BaseLocomotive locomotive) {
        if (_Forward == null) {
            _Forward = true;
            _RoadMode = true;
            _MaxSpeed = 45;
        }

        builder.RebuildOnEvent<SelectedSwitchChanged>();

        // ReSharper disable once StringLiteralTypo
        builder.AddField("Direction".Color("888888"), 
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Forward", _Forward == true, () => SetDirection(true));
                strip.AddButtonSelectable("Backward", _Forward == false, () => SetDirection(false));
            })!
        );

        builder.AddField("AutoEngineerMode".Color("999999"),
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

        builder.AddField("Stop location", 
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Before switch", _StopMode == StopMode.BeforeSwitch, () => SetStopMode(StopMode.BeforeSwitch));
                strip.AddButtonSelectable("After switch", _StopMode == StopMode.AfterSwitch, () => SetStopMode(StopMode.AfterSwitch));
                strip.AddButtonSelectable("End of track", _StopMode == StopMode.EndOfTrack, () => SetStopMode(StopMode.EndOfTrack));
                strip.AddButtonSelectable("Car lengths", _StopMode == StopMode.CarLengths, () => SetStopMode(StopMode.CarLengths));
                strip.AddButtonSelectable("Couple", _StopMode == StopMode.Couple, () => SetStopMode(StopMode.Couple));
            })!
        );

        switch (_StopMode) {
            case StopMode.BeforeSwitch:
            case StopMode.AfterSwitch:
                builder.AddField("Switch", builder.AddInputField(_Id ?? "", o => _Id = o, "You can select Id by clicking on switch")!);

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
                builder.AddField("Car Lengths".Color("aaaaaa"),
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
*/