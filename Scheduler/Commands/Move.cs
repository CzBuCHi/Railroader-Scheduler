using System.Collections.Generic;
using System.Linq;
using Game.Messages;
using Model;
using Model.AI;
using Newtonsoft.Json;
using Scheduler.Commands.Abstract;
using Scheduler.Data;
using Track;
using UI.Builder;
using UI.EngineControls;
using UnityEngine;

namespace Scheduler.Commands;

public sealed class ScheduleCommandMove(bool forward, bool before, int switchCount, int? maxSpeed) : ScheduleCommandBase
{
    public override string Identifier => "Move";

    public bool Forward { get; } = forward;
    public bool Before { get; } = before;
    public int? MaxSpeed { get; } = maxSpeed;
    public int SwitchCount { get; } = switchCount;

    public override string ToString() {
        return
            $"Move {(Forward ? "forward" : "back")} at {(MaxSpeed == null ? "yard Speed" : $"max. speed {MaxSpeed} MPH")} and " +
            $"stop {(Before ? "before" : "after")} {GetOrdinal(SwitchCount)} switch.";
    }

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

    public override void Execute(BaseLocomotive locomotive) {
        if (Before) {
            SchedulerPlugin.DebugMessage("Executing order to stop before next switch");
        }

        var persistence = new AutoEngineerPersistence(locomotive.KeyValueObject);
        var orders = persistence.Orders;
        var coupledCars = locomotive.EnumerateCoupled(orders.Forward ? Car.End.F : Car.End.R).ToList();
        var trainLength = coupledCars.Sum(car => car.carLength) + 1f * (coupledCars.Count - 1); // add 1m separation per car

        SchedulerPlugin.DebugMessage($"Found locomotive {locomotive.DisplayName} with {coupledCars.Count} cars");

        var startLocation = SchedulerUtility.FirstCarLocation(locomotive, Forward ? Car.End.F : Car.End.R);

        var items = new List<(TrackSegment Segment, TrackNode Node)>();
        var index = 0;
        foreach (var item in SchedulerUtility.GetRoute(startLocation)) {
            items.Add(item);
            if (Graph.Shared!.IsSwitch(item.Node)) {
                ++index;
            }

            if (index == SwitchCount) {
                break;
            }
        }

        foreach (var (_, node) in items) {
            SchedulerPlugin.DebugMessage($"NODE: {node.id}");
        }

        var distance = SchedulerUtility.Distance(startLocation, items);

        if (!Before) {
            var (segment, node) = items.Last();
            var extraDistance = trainLength;

            // add extra segments after target node to fit train
            while (extraDistance > 0) {
                if (!SchedulerUtility.GetNextSegmentOnRoute(ref segment, ref node)) {
                    return;
                }

                extraDistance -= segment.GetLength();
                items.Add((segment, node));
            }

            distance += trainLength;
        }

        SchedulerPlugin.DebugMessage($"Distance: {distance}");

        var moved = Graph.Shared!.LocationByMoving(startLocation, distance);
        SchedulerPlugin.DebugMessage($"moved: {moved}");

        var (lastSegment, lastNode) = items.Last();

        var foulingDistance = Graph.Shared!.CalculateFoulingDistance(lastNode);
        SchedulerPlugin.DebugMessage($"FoulingDistance: {foulingDistance}");

        if (Graph.Shared.DecodeSwitchAt(lastNode, out var enter, out _, out _) && lastSegment != enter) {
            distance -= foulingDistance + 6.1f;
        }

        SchedulerPlugin.DebugMessage($"Final distance: {distance}");

        var helper = new AutoEngineerOrdersHelper(locomotive, persistence);
        helper.SetOrdersValue(MaxSpeed == null ? AutoEngineerMode.Yard : AutoEngineerMode.Road, Forward, MaxSpeed, distance);
    }

    public override CustomYieldInstruction WaitBefore() {
        return new WaitForSecondsRealtime(4f);
    }

    public override WaitUntil WaitUntilComplete(BaseLocomotive locomotive) {
        return new WaitUntil(() => locomotive.IsStopped(1f));
    }

    public override IScheduleCommand Clone() {
        return new ScheduleCommandMove(Forward, Before, SwitchCount, MaxSpeed);
    }
}

public sealed class ScheduleCommandMoveSerializer : ScheduleCommandSerializerBase<ScheduleCommandMove>
{
    private bool? _Forward;
    private bool? _Before;
    private int? _SwitchCount;
    private int? _MaxSpeed;

    protected override void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer) {
        switch (propertyName) {
            case "Forward":
                _Forward = serializer.Deserialize<bool>(reader);
                break;

            case "Before":
                _Before = serializer.Deserialize<bool>(reader);
                break;

            case "SwitchCount":
                _SwitchCount = serializer.Deserialize<int>(reader);
                break;

            case "MaxSpeed":
                _MaxSpeed = serializer.Deserialize<int>(reader);
                break;
        }
    }

    protected override ScheduleCommandMove BuildScheduleCommand() {
        ThrowIfNull(_Forward, "Forward");
        ThrowIfNull(_Before, "Before");
        ThrowIfNull(_SwitchCount, "SwitchCount");

        return new ScheduleCommandMove(_Forward!.Value, _Before!.Value, _SwitchCount!.Value, _MaxSpeed);
    }

    public override void Write(JsonWriter writer, ScheduleCommandMove value) {
        writer.WritePropertyName("Forward");
        writer.WriteValue(value.Forward);

        writer.WritePropertyName("Before");
        writer.WriteValue(value.Before);

        writer.WritePropertyName("SwitchCount");
        writer.WriteValue(value.SwitchCount);

        if (value.MaxSpeed != null) {
            writer.WritePropertyName("MaxSpeed");
            writer.WriteValue(value.MaxSpeed.Value);
        }
    }
}

public sealed class ScheduleCommandMovePanelBuilder : ScheduleCommandPanelBuilderBase
{
    private bool _Direction;
    private bool _RoadMode;
    private bool _SwitchLocation;
    private int _MaxSpeed;
    private int _SwitchCount;

    public override void BuildPanel(UIPanelBuilder builder) {
        // ReSharper disable once StringLiteralTypo
        builder.AddField("Direction".Color("cccccc")!,
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Forward", _Direction, () => SetToggle(ref _Direction, true));
                strip.AddButtonSelectable("Backward", !_Direction, () => SetToggle(ref _Direction, false));
            })!
        );
        builder.AddField("Mode",
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Road", _RoadMode, () => SetToggle(ref _RoadMode, true));
                strip.AddButtonSelectable("Yard", !_RoadMode, () => SetToggle(ref _RoadMode, false));
            })!
        );
        if (_RoadMode) {
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
                strip.AddButtonSelectable("Before", _SwitchLocation, () => SetToggle(ref _SwitchLocation, true));
                strip.AddButtonSelectable("After", !_SwitchLocation, () => SetToggle(ref _SwitchLocation, false));
            })!
        );
        builder.AddField("Switches",
            builder.ButtonStrip(strip => {
                    strip.AddField("Count", $"{_SwitchCount}");
                    if (_SwitchCount > 0) {
                        strip.AddButton("-1", () => {
                            --_SwitchCount;
                            strip.Rebuild();
                        });
                    }

                    strip.AddButton("+1", () => {
                        ++_SwitchCount;
                        strip.Rebuild();
                    });
                }
            )!
        );

        return;

        void SetToggle(ref bool field, bool value) {
            if (field == value) {
                return;
            }

            field = value;
            builder.Rebuild();
        }
    }

    public override IScheduleCommand CreateScheduleCommand() {
        return new ScheduleCommandMove(_Direction, _SwitchLocation, _SwitchCount, _RoadMode ? _MaxSpeed : null);
    }
}
