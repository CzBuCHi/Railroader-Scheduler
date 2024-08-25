using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Model;
using Scheduler.Extensions;
using Serilog;
using Track;
using static UI.SwitchList.OpsCarList.Entry;
using Location = Track.Location;

namespace Scheduler;


public static class SchedulerUtility
{
    public static bool CanOperateSwitch(TrackNode node, BaseLocomotive locomotive) {
        if (node is { IsCTCSwitch: true, IsCTCSwitchUnlocked: false }) {
            global::UI.Console.Console.shared!.AddLine($"AI Engineer {Hyperlink.To(locomotive)}: Switch controlled and locked by CTC. Call dispatcher.");
            return false;
        }

        return true;
    }

    public static int ResolveTrainCars(BaseLocomotive locomotive, out List<string> trainCars, out List<int> trainCarsPositions, bool includeLocomotive) {
        var consist = locomotive.EnumerateConsist();
        if (!includeLocomotive) {
            consist = consist.Where(o => o.Position != 0);
        }

        var carIndices = consist.ToArray();
        var locomotiveIndex = Array.FindIndex(carIndices, tuple => tuple.Car == locomotive);
        trainCars = carIndices.Select(o => o.Position == 0 ? $"Locomotive ({o.Car!.DisplayName})" : $"Car #{o.Position} ({o.Car!.DisplayName})").ToList();
        trainCarsPositions = carIndices.Select(o => o.Position).ToList();
        return locomotiveIndex;
    }

    private static readonly Serilog.ILogger _Logger = Log.ForContext(typeof(SchedulerUtility))!;

    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    public static FinalRoute? GetDistanceToSwitch(Location startLocation, TrackNode targetSwitch) {

        _Logger.Information("GetDistanceToSwitch: from " + startLocation + " to " + targetSwitch);

        var segment = startLocation.segment;
        Queue<QueueItem> queue = new();
        EnqueueFirst(segment, segment.a);
        EnqueueFirst(segment, segment.b);

        while (queue.Count > 0) {
            _Logger.Information($"queue count: {queue.Count}");
            var (entrySegment, route, distance, fouling) = queue.Dequeue();
            if (route.Length > 50) {
                _Logger.Information("route too long aborting search");
                continue;
            }

            _Logger.Information($"Distance: {distance} Node count: ({route.Length}) Segment: {entrySegment}");

            var lastNode = route.Last();
            if (lastNode == targetSwitch) {
                _Logger.Information("found target switch");

                var location = Graph.Shared.LocationFrom(entrySegment, TrackSegment.End.A)!;
                var finalDistance = distance;
                if (fouling) {
                    
                    var foulingDistance = Graph.Shared.CalculateFoulingDistance(lastNode);
                    _Logger.Information("fouling, adding " + foulingDistance);

                    location = Graph.Shared.LocationByMoving(location.Value, foulingDistance);
                    finalDistance += foulingDistance;
                }

                _Logger.Error($"finalDistance: {finalDistance}");
                return new FinalRoute(finalDistance, route, location.Value);
            }

            var segments = Graph.Shared.SegmentsConnectedTo(lastNode).Where(o => o != entrySegment).ToArray();
            if (segments.Length == 0) {
                _Logger.Information("end of track");
                // end of track
                continue;
            }

            fouling = false;
            TrackSegment[] choices;
            if (segments.Length == 1) {
                // simple node
                _Logger.Information("simple node");
                choices = segments;
            } else if (segments.Length == 2) {
                // switch
                _Logger.Information("switch");
                Graph.Shared.DecodeSwitchAt(lastNode, out var enter, out var a, out var b);
                if (entrySegment == enter) {
                    // -<=
                    choices = [a, b];
                } else {
                    // =>-
                    choices = [enter];
                    fouling = true;
                }
            } else {
                _Logger.Error("invalid number of segments");
                // invalid number of segments
                return null;
            }

            foreach (var choice in choices) {
                var node = choice.GetOtherNode(lastNode);
                queue.Enqueue(new QueueItem(choice, [..route, node], distance + choice.GetLength(), fouling));
            }
        }
        return null;

        void EnqueueFirst(TrackSegment trackSegment, TrackNode node) {
            var fouling = Graph.Shared.DecodeSwitchAt(node, out var enter, out _, out _) && trackSegment == enter;
            queue.Enqueue(new QueueItem(trackSegment, [node], startLocation.DistanceTo(node), fouling));
        }
    }


    public static Location GetCarLocation(Car car, Car.End end) {
        var logical = car.EndToLogical(end);
        return logical == Car.LogicalEnd.A ? car.LocationA : car.LocationB.Flipped();
    }

    public record FinalRoute(float Distance, TrackNode[] Nodes, Location Location);

    private record QueueItem(TrackSegment EntrySegment, TrackNode[] Nodes, float Distance, bool Fouling);

}
