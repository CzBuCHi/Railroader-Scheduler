using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Game.Messages;
using Model;
using Model.AI;
using Newtonsoft.Json;
using Scheduler.HarmonyPatches;
using Scheduler.Utility;
using Scheduler.Visualizers;
using Serilog;
using Track;
using UI.Builder;
using UI.EngineControls;
using UnityEngine;
using ILogger = Serilog.ILogger;

namespace Scheduler.Commands;

public class Move(Direction direction, AutoEngineerMode autoEngineerMode, int? maxRoadSpeed, StopMode stopMode, int? carLengths) : ICommand
{
    public Direction Direction { get; } = direction;
    public AutoEngineerMode AutoEngineerMode { get; } = autoEngineerMode;
    public int? MaxRoadSpeed { get; } = maxRoadSpeed;
    public StopMode StopMode { get; } = stopMode;
    public int? CarLengths { get; } = carLengths;

    public string DisplayText => BuildDisplayText();

    private string BuildDisplayText() {
        var sb = new StringBuilder();
        sb.Append("Move ")
          .Append(Direction.ToString().ToLower())
          .Append(" at ")
          .Append(AutoEngineerMode == AutoEngineerMode.Yard ? "yard speed" : $"max speed {MaxRoadSpeed} MPH")
          .Append(", stop ");

        switch (StopMode) {
            case StopMode.CarLengths:
                sb.Append("after ")
                  .Append(CarLengths!.Value)
                  .Append(" car lengths.");
                break;
            default:
                throw new NotSupportedException();
        }

        return sb.ToString();
    }
}

public enum Direction
{
    Forward,
    Backward
}

public enum StopMode
{
    CarLengths
}

public sealed class MoveManager : CommandManager<Move>
{
    private static readonly ILogger _Logger = Log.ForContext(typeof(MoveManager))!;

    public override IEnumerator Execute(Dictionary<string, object> state) {
        var locomotive = (BaseLocomotive)state["locomotive"]!;

        var (startLocation, targetLocation, distance) = GetTargetLocationAndDistance(locomotive);

        if (SchedulerPlugin.Settings.Debug) {
            SchedulerPlugin.ShowLocationVisualizer(startLocation, Color.green);
            SchedulerPlugin.ShowLocationVisualizer(targetLocation, Color.cyan);
        }

        _Logger.Information($"  move distance {distance}m");

        var persistence = new AutoEngineerPersistence(locomotive.KeyValueObject!);
        var helper = new AutoEngineerOrdersHelper(locomotive, persistence);
        helper.SetOrdersValue(Command!.AutoEngineerMode, Command.Direction == Direction.Forward, Command.MaxRoadSpeed, distance);

        _Logger.Information("  waiting for AI to start moving ...");

        // wait for AI to start moving ...
        yield return new WaitWhile(() => locomotive.IsStopped());

        yield return new WaitForSecondsRealtime(0.5f);

        _Logger.Information("  waiting for AI to stop moving ...");

        // wait until AI stops ...
        var stopMove = false;
        var observer = persistence.ObserveOrders(_ => stopMove = true, false);
        yield return new WaitUntil(() => {
            var manualStopDistance = locomotive.AutoEngineerPlanner!.GetManualStopDistance();
            return stopMove || manualStopDistance < 0.01f;
        });

        observer.Dispose();

        // if orders changed break schedule execution ...
        if (stopMove) {
            state["stop"] = true;
            yield break;
        }

        if (Command.AutoEngineerMode == AutoEngineerMode.Road) {
            // put train to manual mode (otherwise UI will show Road mode, when train is not moving)
            helper.SetOrdersValue(AutoEngineerMode.Road, Command.Direction == Direction.Forward, 0, 0);
            helper.SetOrdersValue(AutoEngineerMode.Off);
        }

        _Logger.Information("  move completed ...");
        if (distance > 200) {
            state["wage"] = (int)state["wage"] + (int)Math.Ceiling(distance / 1000f);
        }
    }

    private (Location StartLocation, Location TargetLocation, float Distance) GetTargetLocationAndDistance(BaseLocomotive locomotive) {
        Location startLocation;
        Location targetLocation;
        float distance;
       
        switch (Command!.StopMode) {
            case StopMode.CarLengths:
                startLocation = Command!.Direction == Direction.Forward ? locomotive.LocationR.Flipped() : locomotive.LocationF;
                distance = Command.CarLengths!.Value * 12.2f;
                targetLocation = Graph.Shared.LocationByMoving(startLocation, (Command!.Direction == Direction.Forward ? 1 : -1) * distance);
                break;
            default:
                throw new NotImplementedException();
        }

        return (startLocation, targetLocation, distance);
    }


    protected override object TryCreateCommand() {
        List<string> missing = new();
        if (_Direction == null) {
            missing.Add("Direction");
        }

        if (_AutoEngineerMode == null) {
            missing.Add("AutoEngineerMode");
        }

        if (_AutoEngineerMode == AutoEngineerMode.Road && _MaxRoadSpeed == null) {
            missing.Add("MaxRoadSpeed");
        }

        if (_StopMode == null) {
            missing.Add("StopMode");
        }

        switch (_StopMode) {
            case StopMode.CarLengths:
                if (_CarLengths == null) {
                    missing.Add("CarLengths");
                }

                break;
        }

        if (missing.Count > 0) {
            return $"Missing mandatory property '{string.Join(", ", missing)}'.";
        }

        return new Move(_Direction!.Value, _AutoEngineerMode!.Value, _MaxRoadSpeed, _StopMode!.Value, _CarLengths);
    }

    public override bool ShowTrackSwitchVisualizers { get; } = false;

    protected override void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer) {
        switch (propertyName) {
            case nameof(Move.Direction):
                _Direction = (Direction)serializer.Deserialize<int>(reader);
                break;
            case nameof(Move.AutoEngineerMode):
                _AutoEngineerMode = (AutoEngineerMode)serializer.Deserialize<int>(reader);
                break;
            case nameof(Move.MaxRoadSpeed):
                _MaxRoadSpeed = serializer.Deserialize<int>(reader);
                break;
            case nameof(Move.StopMode):
                _StopMode = (StopMode)serializer.Deserialize<int>(reader);
                break;
            case nameof(Move.CarLengths):
                _CarLengths = serializer.Deserialize<int>(reader);
                break;
        }
    }

    public override void SerializeProperties(JsonWriter writer) {
        WriteProperty(nameof(Command.Direction), (int)Command!.Direction);
        WriteProperty(nameof(Command.AutoEngineerMode), (int)Command.AutoEngineerMode);
        if (Command.AutoEngineerMode == AutoEngineerMode.Road) {
            WriteProperty(nameof(Command.MaxRoadSpeed), Command.MaxRoadSpeed!.Value);
        }

        WriteProperty(nameof(Command.StopMode), (int)Command.StopMode);

        switch (Command.StopMode) {
            case StopMode.CarLengths:
                WriteProperty(nameof(Command.CarLengths), Command.CarLengths!.Value);
                break;
        }

        return;

        void WriteProperty(string name, object value) {
            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }
    }

    private Direction? _Direction;
    private AutoEngineerMode? _AutoEngineerMode;
    private int? _MaxRoadSpeed;
    private StopMode? _StopMode;
    private int? _CarLengths;

    public override void BuildPanel(UIPanelBuilder builder, BaseLocomotive locomotive) {
        if (_Direction == null) {
            _Direction = Direction.Forward;
            _AutoEngineerMode = AutoEngineerMode.Yard;
            _StopMode = StopMode.CarLengths;
            _CarLengths = 1;
        }

        builder.AddField("Direction".Color("888888"),
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Forward", _Direction == Direction.Forward, () => SetDirection(Direction.Forward));
                strip.AddButtonSelectable("Backward", _Direction == Direction.Backward, () => SetDirection(Direction.Backward));
            })!
        );

        builder.AddField("AutoEngineerMode".Color("888888"),
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Road", _AutoEngineerMode == AutoEngineerMode.Road, () => SetAutoEngineerMode(AutoEngineerMode.Road));
                strip.AddButtonSelectable("Yard", _AutoEngineerMode == AutoEngineerMode.Yard, () => SetAutoEngineerMode(AutoEngineerMode.Yard));
            })!
        );

        if (_AutoEngineerMode == AutoEngineerMode.Road) {
            builder.AddField("Max Speed",
                builder.AddSliderQuantized(() => _MaxRoadSpeed ?? 0,
                    () => (_MaxRoadSpeed ?? 0).ToString("0"),
                    o => _MaxRoadSpeed = (int)o, 5, 0, 45,
                    o => _MaxRoadSpeed = (int)o
                )!
            );
        }

        builder.AddField("Stop behavior",
            builder.ButtonStrip(strip => {
                strip.AddButtonSelectable("Car lengths", _StopMode == StopMode.CarLengths, () => SetStopMode(StopMode.CarLengths));
            })!
        );

        switch (_StopMode) {
            case StopMode.CarLengths:
                builder.AddField("Car Lengths".Color("888888"),
                    builder.ButtonStrip(strip => {
                        strip.AddButtonSelectable("1", _CarLengths == 1, () => SetCarLengths(1));
                        strip.AddButtonSelectable("2", _CarLengths == 2, () => SetCarLengths(2));
                        strip.AddButtonSelectable("5", _CarLengths == 5, () => SetCarLengths(5));
                        strip.AddButtonSelectable("10", _CarLengths == 10, () => SetCarLengths(10));
                        strip.AddButtonSelectable("20", _CarLengths == 20, () => SetCarLengths(20));
                    }, 4)!
                );
                break;
        }

        return;

        void SetDirection(Direction direction) {
            if (_Direction == direction) {
                return;
            }

            _Direction = direction;
            builder.Rebuild();
        }
        void SetAutoEngineerMode(AutoEngineerMode mode) {
            if (_AutoEngineerMode == mode) {
                return;
            }

            _AutoEngineerMode = mode;
            builder.Rebuild();
        }
        void SetStopMode(StopMode stopMode) {
            if (_StopMode == stopMode) {
                return;
            }

            _StopMode = stopMode;
            builder.Rebuild();
        }
        void SetCarLengths(int carLengths) {
            if (_CarLengths == carLengths) {
                return;
            }

            _CarLengths = carLengths;
            builder.Rebuild();
        }
    }
}
