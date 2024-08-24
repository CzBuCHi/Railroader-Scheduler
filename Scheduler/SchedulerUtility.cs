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

    public static void ResolveTrainCars(BaseLocomotive locomotive, out List<string> trainCars, out List<int> trainCarsPositions, bool includeLocomotive) {
        var consist = locomotive.EnumerateConsist();
        if (!includeLocomotive) {
            consist = consist.Where(o => o.Position != 0);
        }

        var carIndices = consist.ToArray();
        trainCars = carIndices.Select(o => o.Position == 0 ? $"Locomotive ({o.Car!.DisplayName})" : $"Car #{o.Position} ({o.Car!.DisplayName})").ToList();
        trainCarsPositions = carIndices.Select(o => o.Position).ToList();
    }

    private static readonly Serilog.ILogger _Logger = Log.ForContext(typeof(SchedulerUtility))!;

    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    public static FinalRoute? GetDistanceToSwitch(Location startLocation, TrackNode targetSwitch) {

        _Logger.Information("GetDistanceToSwitch: from " + startLocation + " to " + targetSwitch);

        var segment = startLocation.segment;
        Queue<QueueItem> queue = new();
        queue.Enqueue(new QueueItem(segment, [segment.a], startLocation.DistanceTo(segment.a)));
        queue.Enqueue(new QueueItem(segment, [segment.b], startLocation.DistanceTo(segment.b)));

        while (queue.Count > 0) {
            _Logger.Information($"queue count: {queue.Count}");
            var (entrySegment, route, distance) = queue.Dequeue();

            if (route.Length > 50) {
                _Logger.Information("route too long aborting search");
                continue;
            }

            _Logger.Information($"Distance: {distance} Node count: ({route.Length}) Segment: {entrySegment}");

            var lastNode = route.Last();

            var segments = Graph.Shared.SegmentsConnectedTo(lastNode).Where(o => o != entrySegment).ToArray();
            if (segments.Length == 0) {
                _Logger.Information("end of track");
                // end of track
                continue;
            }

            bool fouling = false;
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
                var nextDistance = distance + choice.GetLength();
                TrackNode[] nextRoute = [..route, node];
                if (node == targetSwitch) {
                    _Logger.Error("found target switch");

                    var location =   Graph.Shared.LocationFrom(choice, TrackSegment.End.A)!;
                    

                    var finalDistance = nextDistance;
                    if (fouling) {
                        var foulingDistance = Graph.Shared.CalculateFoulingDistance(node);
                        location = Graph.Shared.LocationByMoving(location.Value, foulingDistance);
                        finalDistance -= foulingDistance;
                    }

                    _Logger.Error($"finalDistance: {finalDistance}");
                    return new FinalRoute(finalDistance, nextRoute, location.Value);
                }
                
                queue.Enqueue(new QueueItem(choice, nextRoute, nextDistance));
            }
        }
        return null;
    }


    public static Location GetCarLocation(Car car, Car.End end) {
        var logical = car.EndToLogical(end);
        return logical == Car.LogicalEnd.A ? car.LocationA : car.LocationB.Flipped();
    }

    public record FinalRoute(float Distance, TrackNode[] Nodes, Location Location);

    private record QueueItem(TrackSegment EntrySegment, TrackNode[] Nodes, float Distance);

}
