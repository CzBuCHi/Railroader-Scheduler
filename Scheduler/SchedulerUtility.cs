namespace Scheduler;

using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Game;
using global::UI.Common;
using Model;
using Model.AI;
using Network.Messages;
using Scheduler.Extensions;
using Scheduler.HarmonyPatches;
using Track;
using UnityEngine;

public static class SchedulerUtility {

    public static Location StartLocation(bool forward, BaseLocomotive baseLocomotive) {
        var end = forward ? Car.End.F : Car.End.R;
        var logical = baseLocomotive.EndToLogical(end);
        var car = baseLocomotive.EnumerateCoupled(logical)!.First();
        var num = logical == Car.LogicalEnd.A ? 1 : 0;
        var location = num != 0 ? car.LocationA : car.LocationB;
        return num == 0 ? location.Flipped() : location;
    }

    public static TrackNode GetNextSwitch(TrackNode node, TrackSegment startSegment) {
        var graph = TrainController.Shared!.graph!;
        for (var index = 0; index < 50; ++index) {
            if (graph.NodeIsDeadEnd(node, out _)) {
                return node;
            }

            if (graph.IsSwitch(node)) {
                return node;
            }

            var segment = startSegment;
            foreach (var trackSegment2 in graph.SegmentsConnectedTo(node)!) {
                if (!(trackSegment2 == startSegment)) {
                    segment = trackSegment2;
                }
            }

            if (segment != null) {
                var end2 = new Location(segment, 0.0f, segment.a == node ? TrackSegment.End.A : TrackSegment.End.B).EndIsA ? TrackSegment.End.B : TrackSegment.End.A;
                startSegment = segment;
                node = startSegment.NodeForEnd(end2)!;
            }
        }

        return node;
    }

    public static TrackNode GetNextSwitchByLocation(Location startLocation) {
        var startSegment = startLocation.segment!;
        var node = startSegment.NodeForEnd(startLocation.EndIsA ? TrackSegment.End.B : TrackSegment.End.A)!;
        return GetNextSwitch(node, startSegment);
    }

    public static bool CanOperateSwitch(TrackNode node, Location startLocation, BaseLocomotive locomotive) {
        if (node is { IsCTCSwitch: true, IsCTCSwitchUnlocked: false }) {
            global::UI.Console.Console.shared!.AddLine($"AI Engineer {Hyperlink.To(locomotive)}: Switch controlled and locked by CTC.");
            return false;
        }

        float? distance = null;
        if (startLocation.segment!.a == node || startLocation.segment.b == node) {
            distance = startLocation.DistanceTo(node);
        }

        if (distance != null && distance > Graph.Shared!.CalculateFoulingDistance(node) * 2) {
            global::UI.Console.Console.shared!.AddLine($"AI Engineer {Hyperlink.To(locomotive)}: Switch is too far. I`m not walking there.");
            return false;
        }

        return true;
    }

    public static void ResolveTrainCars(BaseLocomotive locomotive, out List<string> trainCars, out List<int> trainCarsPositions) {
        var carIndices = locomotive.EnumerateConsist()
            .Where(o => o.Car != locomotive) // skip locomotive
            .ToArray();

        trainCars = carIndices.Select(o => $"Car #{o.Position} ({o.Car!.DisplayName})").ToList();
        trainCarsPositions = carIndices.Select(o => o.Position).ToList();
    }

    private static Location StartLocation(BaseLocomotive locomotive, List<Car> coupledCarsCached, bool forward) {
        var logical = (int)locomotive.EndToLogical(forward ? Car.End.F : Car.End.R);
        var car = coupledCarsCached[0]!;
        if (logical == 0) {
            var locationA = car.LocationA;
            return !locationA.IsValid ? car.WheelBoundsA : locationA;
        }

        var locationB = car.LocationB;
        return (locationB.IsValid ? locationB : car.WheelBoundsB).Flipped();
    }

    public static float? GetDistanceForSwitchOrder(int switchesToFind, bool clearSwitchesUnderTrain, bool stopBeforeSwitch, BaseLocomotive locomotive, AutoEngineerPersistence persistence) {
        const float carLengthInMeters = 12.2f;

        if (stopBeforeSwitch) {
            SchedulerPlugin.DebugMessage("Executing order to stop before next switch");
        } else if (clearSwitchesUnderTrain) {
            var str = switchesToFind == 1 ? "first switch" : $"{switchesToFind} switches";
            SchedulerPlugin.DebugMessage($"Executing order to stop after clearing {str}");
        } else {
            SchedulerPlugin.DebugMessage("Executing order to stop after closest switch in front of train");
        }

        var graph = Graph.Shared!;
        var orders = persistence.Orders;

        var coupledCars = locomotive.EnumerateCoupled(orders.Forward ? Car.End.F : Car.End.R)!.ToList();
        var totalLength = coupledCars.Sum(car => car.carLength) + 1f * (coupledCars.Count - 1); // add 1m separation per car

        SchedulerPlugin.DebugMessage($"Found locomotive {locomotive.DisplayName} with {coupledCars.Count} cars");

        // if we are stopping before the next switch then we can look forward from the logical front the train to find the next switch
        var start = StartLocation(locomotive, coupledCars, orders.Forward);

        if (start == null) {
            SchedulerPlugin.DebugMessage("Error: couldn't find locomotive start location");
        }

        SchedulerPlugin.DebugMessage($"Start location: segment ID {start.segment.id}, distance {start.distance} ({start.end})");
        if (clearSwitchesUnderTrain) {
            // if we are clearing a switch, the train might currently be over it.
            // so we want to start our search from the end of the train
            start = graph.LocationByMoving(start.Flipped(), totalLength).Flipped();

            SchedulerPlugin.DebugMessage($"location for end of train: segment ID {start.segment.id}, distance {start.distance} ({start.end})");
        }

        var segment = start.segment;
        var segmentEnd = start.EndIsA ? TrackSegment.End.B : TrackSegment.End.A;

        float distanceInMeters = 0;

        var switchesFound = 0;
        var foundAllSwitches = false;
        var safetyMargin = 2; // distance to leave clear of switch
        var maxSegmentsToSearch = 50;

        for (var i = 0; i < maxSegmentsToSearch; i++) {
            if (i == 0) {
                SchedulerPlugin.DebugMessage($"Adding distance from start to next node {i + 2} {start.DistanceUntilEnd()}");
                distanceInMeters += start.DistanceUntilEnd();
            } else {
                SchedulerPlugin.DebugMessage($"Adding distance from node {i + 1} to next node {i + 2} {segment.GetLength()}");
                distanceInMeters += segment.GetLength();
            }

            var node = segment.NodeForEnd(segmentEnd);
            if (node == null) {
                SchedulerPlugin.DebugMessage("Next node is null");
                break;
            }

            if (graph.IsSwitch(node)) {
                SchedulerPlugin.DebugMessage($"Found next switch at {distanceInMeters}m");

                switchesFound += 1;
                foundAllSwitches = switchesFound >= switchesToFind;

                if (foundAllSwitches) {
                    break;
                }

                // update segments if looking past switch

                // for switches, we need to work out which way it is going
                graph.DecodeSwitchAt(node, out var switchEnterSegment, out var switchExitNormal, out var switchExitReverse);
                
                // if we are coming from a switch exit, the next segment is the switch entrance
                if (segment != switchEnterSegment) {
                    SchedulerPlugin.DebugMessage("Switch only has one exit");
                    segment = switchEnterSegment;
                } else {
                    // otherwise depends on if the switch is thrown
                    if (node.isThrown) {
                        SchedulerPlugin.DebugMessage("Following thrown exit");
                        segment = switchExitReverse;
                    } else {
                        SchedulerPlugin.DebugMessage("Following normal exit");
                        segment = switchExitNormal;
                    }
                }
            } else {
                // next segment for non-switches
                graph.SegmentsReachableFrom(segment, segmentEnd, out var segmentExitNormal, out _);
                segment = segmentExitNormal;
            }

            if (segment == null) {
                SchedulerPlugin.DebugMessage("Next segment is null");
                break;
            }

            // next segment end is whatever end is NOT pointing at the current node
            segmentEnd = segment.NodeForEnd(TrackSegment.End.A)!.id == node.id ? TrackSegment.End.B : TrackSegment.End.A;
        }

        if (foundAllSwitches) {
            var node = segment!.NodeForEnd(segmentEnd)!;

            graph.DecodeSwitchAt(node, out var switchEnterSegment, out _, out _);
            var nodeFoulingDistance = graph.CalculateFoulingDistance(node);

            var facingSwitchEntrance = switchEnterSegment == segment;

            if (stopBeforeSwitch) {
                if (!facingSwitchEntrance) {
                    SchedulerPlugin.DebugMessage($"Subtracting extra distance {nodeFoulingDistance} to not block other track entering switch");
                    distanceInMeters -= nodeFoulingDistance;
                } else {
                    distanceInMeters -= safetyMargin;
                }
            } else {
                if (facingSwitchEntrance) {
                    SchedulerPlugin.DebugMessage($"Adding extra distance {nodeFoulingDistance}m to not block other track entering switch");
                    distanceInMeters += nodeFoulingDistance;
                } else {
                    distanceInMeters += safetyMargin;
                }

                // if we're not stopping before the switch, then we calculated the distance to the switch from
                // the front of the train and therefore need to add the train length to pass the next switch
                if (!clearSwitchesUnderTrain) {
                    distanceInMeters += totalLength;
                }
            }
        }

        distanceInMeters = Math.Max(0, distanceInMeters);

        var action = "Reversing";
        if (orders.Forward) {
            action = "Moving forwards";
        }

        var carLengths = Mathf.FloorToInt(distanceInMeters / carLengthInMeters);
        var distanceString = $"{carLengths} car {"length".Pluralize(carLengths)}";

        if (foundAllSwitches) {
            if (stopBeforeSwitch) {
                Say($"{action} {distanceString} up to switch");
            } else {
                var str = switchesToFind == 1 ? "switch" : $"{switchesToFind} switches";

                Say($"{action} {distanceString} to clear {str}");
            }
        } else {
            Say($"{action} {distanceString}");
        }

        return distanceInMeters;
    }

    private static void Say(string message) {
        var alert = new Alert(AlertStyle.Console, message, TimeWeather.Now.TotalSeconds);
        WindowManager.Shared!.Present(alert);
    }

}