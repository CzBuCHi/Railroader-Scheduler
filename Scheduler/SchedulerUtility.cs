using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Model;
using Model.AI;
using Scheduler.Extensions;
using Scheduler.HarmonyPatches;
using Scheduler.Visualizers;
using Track;
using UnityEngine;

namespace Scheduler;

public static class SchedulerUtility
{
    /// <summary>
    ///     Returns segments from <paramref name="startLocation" /> in direction of travel.
    ///         \             /         \
    ///     -----S-----N-----S-----N-----S-----o-----
    ///          ^s4   ^s3   ^s2   ^s1   ^s0   ^Location
    /// </summary>
    public static IEnumerable<(TrackSegment Segment, TrackNode Node)> GetRoute(Location startLocation) {
        var segment = startLocation.segment!;
        var end = startLocation.EndIsA ? TrackSegment.End.B : TrackSegment.End.A;
        var node = segment.NodeForEnd(end);
        while (true) {
            yield return (segment, node);
            if (!GetNextSegmentOnRoute(ref segment, ref node)) {
                yield break;
            }
        }
    }

    /// <summary>
    ///     Returns next node on route.
    ///                {segment}
    ///                ^node
    ///     ---N-------N-------N
    ///        {segment}
    ///        ^node
    /// </summary>
    public static bool GetNextSegmentOnRoute(ref TrackSegment segment, ref TrackNode node) {
        var localSegment = segment;
        if (Graph.Shared!.NodeIsDeadEnd(node, out _)) {
            SchedulerPlugin.DebugMessage($"Node {node.id} is bumper");
            return false;
        }

        if (Graph.Shared.DecodeSwitchAt(node, out var enter, out var normal, out var thrown)) {
            // if we are coming from a switch exit, the next segment is the switch entrance
            if (localSegment != enter) {
                SchedulerPlugin.DebugMessage($"Switch {node.id} only has one exit");
                localSegment = enter;
            }
            else {
                // otherwise depends on if the switch is thrown
                SchedulerPlugin.DebugMessage($"Switch {node.id}: following {(node.isThrown ? "thrown" : "normal")} exit");
                localSegment = node.isThrown ? thrown : normal;
            }
        }
        else {
            // simple node - get other segment
            SchedulerPlugin.DebugMessage($"Node {node.id} is not a switch");
            localSegment = Graph.Shared.SegmentsConnectedTo(node)!.First(o => o != localSegment);
        }

        segment = localSegment;
        node = localSegment.GetOtherNode(node)!;
        return true;
    }

    /// <summary>
    ///     Returns location of first car in train.
    ///     [W][W][L][T][W][W]
    ///     ^F               ^R
    /// </summary>
    public static Location FirstCarLocation(Car car, Car.End end) {
        var logical = car.EndToLogical(end);
        var firstCar = car.EnumerateCoupled(logical)!.First();
        return logical == Car.LogicalEnd.A ? firstCar.LocationA : firstCar.LocationB.Flipped();
    }

    /// <summary>
    ///     ---S-----N-----N-----N-----S---o---
    ///        s3----      s1----
    ///        ^node s2----^node s0------
    ///              ^node       ^node
    ///                                    ^ Location
    ///              +---------------------+ Distance
    /// </summary>
    public static float Distance(Location startLocation, List<(TrackSegment Segment, TrackNode Node)> segments) {
        var first = segments[0];
        if (first.Segment != startLocation.segment) {
            throw new InvalidOperationException("Location is not on first segment");
        }

        return startLocation.DistanceTo(first.Node) + segments.Skip(1).Sum(item => item.Segment!.GetLength());
    }

    /// <summary> Verify, that AI can operate switch. </summary>
    public static bool CanOperateSwitch(TrackNode node, BaseLocomotive locomotive) {
        if (node is { IsCTCSwitch: true, IsCTCSwitchUnlocked: false }) {
            global::UI.Console.Console.shared!.AddLine($"AI Engineer {Hyperlink.To(locomotive)}: Switch controlled and locked by CTC. Call dispatcher.");
            return false;
        }

        return true;
    }

    /// <summary> Returns total length of train. </summary>
    public static float GetConsistLength(Car car) {
        var coupledCars = car.EnumerateCoupled(Car.End.F)!.ToList();
        return coupledCars.Sum(o => o.carLength) + 1f * (coupledCars.Count - 1); // add 1m separation per car
    }

    /// <summary> Resolve train cars and calculate their relative position from locomotive. </summary>
    public static void ResolveTrainCars(BaseLocomotive locomotive, out List<string> trainCars, out List<int> trainCarsPositions) {
        var carIndices = locomotive.EnumerateConsist()
                                   .Where(o => o.Car != locomotive) // skip locomotive
                                   .ToArray();

        trainCars = carIndices.Select(o => $"Car #{o.Position} ({o.Car!.DisplayName})").ToList();
        trainCarsPositions = carIndices.Select(o => o.Position).ToList();
    }





    public static float? GetDistanceForSwitchOrder(int switchesToFind, bool clearSwitchesUnderTrain, bool stopBeforeSwitch, BaseLocomotive locomotive, bool forward, out TrackNode? targetSwitch) {
        targetSwitch = null;

        if (stopBeforeSwitch)
        {
            SchedulerPlugin.DebugMessage("Executing order to stop before next switch");
        }
        else if (clearSwitchesUnderTrain)
        {
            var str = switchesToFind == 1 ? "first switch" : $"{switchesToFind} switches";
            SchedulerPlugin.DebugMessage($"Executing order to stop after clearing {str}");
        }
        else
        {
            SchedulerPlugin.DebugMessage("Executing order to stop after closest switch in front of train");
        }

        var graph = Graph.Shared!;
        
        var coupledCars = locomotive.EnumerateCoupled(forward ? Car.End.F : Car.End.R)!.ToList();
        var totalLength = coupledCars.Sum(car => car.carLength) + 1f * (coupledCars.Count - 1); // add 1m separation per car

        SchedulerPlugin.DebugMessage($"Found locomotive {locomotive.DisplayName} with {coupledCars.Count} cars");

        TrackSegment segment;
        TrackSegment.End segmentEnd;
        Location start;

        // if we are stopping before the next switch then we can look forward from the logical front the train to find the next switch
        start = FirstCarLocation(locomotive, forward ? Car.End.F : Car.End.R);
        //start = StartLocation(locomotive, coupledCars, orders.Forward);

        if (start == null)
        {
            SchedulerPlugin.DebugMessage("Error: couldn't find locomotive start location");
        }

        SchedulerPlugin.DebugMessage($"Start location: segment ID {start.segment.id}, distance {start.distance} ({start.end})");
        if (clearSwitchesUnderTrain)
        {
            // if we are clearing a switch, the train might currently be over it.
            // so we want to start our search from the end of the train
            start = graph.LocationByMoving(start.Flipped(), totalLength).Flipped();

            SchedulerPlugin.DebugMessage($"location for end of train: segment ID {start.segment.id}, distance {start.distance} ({start.end})");
        }

        segment = start.segment;
        segmentEnd = start.EndIsA ? TrackSegment.End.B : TrackSegment.End.A;

        float distanceInMeters = 0;

        var switchesFound = 0;
        var foundAllSwitches = false;
        var safetyMargin = 6; // distance to leave clear of switch
        var maxSegmentsToSearch = 50;

        for (var i = 0; i < maxSegmentsToSearch; i++)
        {
            if (i == 0)
            {
                SchedulerPlugin.DebugMessage($"Adding distance from start to next node {i + 2} {start.DistanceUntilEnd()}");
                distanceInMeters += start.DistanceUntilEnd();
            }
            else
            {
                SchedulerPlugin.DebugMessage($"Adding distance from node {i + 1} to next node {i + 2} {segment.GetLength()}");
                distanceInMeters += segment.GetLength();
            }

            var node = segment.NodeForEnd(segmentEnd);
            if (node == null)
            {
                SchedulerPlugin.DebugMessage("Next node is null");
                break;
            }

            targetSwitch = node;

            if (graph.IsSwitch(node))
            {
                SchedulerPlugin.DebugMessage($"Found next switch at {distanceInMeters}m");

                switchesFound += 1;
                foundAllSwitches = switchesFound >= switchesToFind;

                if (foundAllSwitches)
                {
                    break;
                }

                // update segments if looking past switch

                // for switches we need to work out which way it is going
                graph.DecodeSwitchAt(node, out var switchEnterSegment, out var switchExitNormal, out var switchExitReverse);

                // switchEnterSegment, switchExitSegmentA, switchExitSegmentB cannot be null here, because graph.IsSwitch(node) call above ...

                // if we are coming from a switch exit, the next segment is the switch entrance
                if (switchExitNormal != null && segment.id == switchExitNormal.id || switchExitReverse != null && segment.id == switchExitReverse.id)
                {
                    SchedulerPlugin.DebugMessage("Switch only has one exit");
                    segment = switchEnterSegment;
                }
                else
                {
                    // otherwise depends on if the switch is thrown
                    if (node.isThrown)
                    {
                        SchedulerPlugin.DebugMessage("Following thrown exit");
                        segment = switchExitReverse;
                    }
                    else
                    {
                        SchedulerPlugin.DebugMessage("Following normal exit");
                        segment = switchExitNormal;
                    }
                }
            }
            else
            {
                // next segment for non-switches
                graph.SegmentsReachableFrom(segment, segmentEnd, out var segmentExitNormal, out _);
                segment = segmentExitNormal;
            }

            if (segment == null)
            {
                SchedulerPlugin.DebugMessage("Next segment is null");
                break;
            }

            // next segment end is whatever end is NOT pointing at the current node
            segmentEnd = segment.NodeForEnd(TrackSegment.End.A).id == node.id ? TrackSegment.End.B : TrackSegment.End.A;
        }

        if (foundAllSwitches)
        {
            var node = segment.NodeForEnd(segmentEnd);
            targetSwitch = node;

            graph.DecodeSwitchAt(node, out var switchEnterSegment, out _, out _);
            var nodeFoulingDistance = graph.CalculateFoulingDistance(node);

            var facingSwitchEntrance = switchEnterSegment == segment;

            if (stopBeforeSwitch)
            {
                if (!facingSwitchEntrance)
                {
                    SchedulerPlugin.DebugMessage($"Subtracting extra distance {nodeFoulingDistance} to not block other track entering switch");
                    distanceInMeters = distanceInMeters - nodeFoulingDistance;
                }
                else
                {
                    distanceInMeters -= safetyMargin;
                }
            }
            else
            {
                if (facingSwitchEntrance)
                {
                    SchedulerPlugin.DebugMessage($"Adding extra distance {nodeFoulingDistance}m to not block other track entering switch");
                    distanceInMeters = distanceInMeters + nodeFoulingDistance;
                }
                else
                {
                    distanceInMeters += safetyMargin;
                }

                // if we're not stopping before the switch, then we calculated the distance to the switch from
                // the front of the train and therefore need to add the train length to pass the next switch
                if (!clearSwitchesUnderTrain)
                {
                    distanceInMeters += totalLength;
                }
            }
        }

        return Math.Max(0, distanceInMeters);
    }

    //private static Location StartLocation(BaseLocomotive locomotive, List<Car> coupledCarsCached, bool forward)
    //{
    //    var logical = locomotive.EndToLogical(forward ? Car.End.F : Car.End.R);
    //    var car = coupledCarsCached[0];
    //    if (logical == Car.LogicalEnd.A)
    //    {
    //        var locationA = car.LocationA;
    //        return !locationA.IsValid ? car.WheelBoundsA : locationA;
    //    }

    //    var locationB = car.LocationB;
    //    return (locationB.IsValid ? locationB : car.WheelBoundsB).Flipped();
    //}


    #region move camera to node

    /// <summary>
    /// Camera state before move
    /// </summary>
    private static (Vector3 position, bool isFirstPerson)? _CameraState;

    /// <summary> Move 3rd person camera to selected track node and if player do not move camera then return back to original position after 2 seconds. </summary>
    public static void MoveCameraToNode(TrackNode node) {
        var cameraSelector = CameraSelector.shared!;

        // ignore camera locations when arrow is shown
        _CameraState ??= (cameraSelector.CurrentCameraPosition, cameraSelector.CurrentCameraIsFirstPerson);

        // move camera
        cameraSelector.ZoomToPoint(node.transform!.localPosition);

        var afterMove = cameraSelector.CurrentCameraPosition;
        TrackNodeVisualizer.Shared.OnHidden = () => {
            // move camera back if was not moved
            if (cameraSelector.CurrentCameraPosition == afterMove) {
                if (_CameraState.Value.isFirstPerson) {
                    cameraSelector.SelectCamera(CameraSelector.CameraIdentifier.FirstPerson);
                } else {
                    cameraSelector.ZoomToPoint(_CameraState.Value.position);
                }
            }

            _CameraState = null;
        };

        // show arrow for 2 seconds
        TrackNodeVisualizer.Shared.Show(node);
    } 
    #endregion
}