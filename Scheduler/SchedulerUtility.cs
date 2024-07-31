using System;
using System.Collections.Generic;
using System.Linq;
using Model;
using Scheduler.Extensions;
using Track;

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
    public static bool CanOperateSwitch(TrackNode node, Location startLocation, BaseLocomotive locomotive, List<(TrackSegment Segment, TrackNode Node)> items) {
        if (node is { IsCTCSwitch: true, IsCTCSwitchUnlocked: false }) {
            global::UI.Console.Console.shared!.AddLine($"AI Engineer {Hyperlink.To(locomotive)}: Switch controlled and locked by CTC. Call dispatcher.");
            return false;
        }

        var distance = Distance(startLocation, items);
        var (_, lastNode) = items.Last();
        var foulingDistance = Graph.Shared!.CalculateFoulingDistance(lastNode);

        if (distance > foulingDistance * 2) {
            global::UI.Console.Console.shared!.AddLine($"AI Engineer {Hyperlink.To(locomotive)}: Switch is too far. I`m not walking there.");
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
}