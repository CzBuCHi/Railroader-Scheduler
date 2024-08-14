using System;
using Game.Messages;
using Model;
using Model.AI;
using Newtonsoft.Json;
using Scheduler.Commands.OLD.Abstract;
using Scheduler.Data;
using UI.Builder;
using UI.EngineControls;
using UnityEngine;

namespace Scheduler.Commands.OLD;
[Obsolete]
public sealed class ScheduleCommandMove(bool forward, bool before, int switchCount, int? maxSpeed, bool clearSwitchesUnderTrain) : ScheduleCommandBase
{
    public override string Identifier => "Move";

    public bool Forward { get; } = forward;
    public bool Before { get; } = before;
    public int? MaxSpeed { get; } = maxSpeed;
    public int SwitchCount { get; } = switchCount;
    public bool ClearSwitchesUnderTrain { get; } = clearSwitchesUnderTrain;

    public override string ToString()
    {
        return
            $"Move {(Forward ? "forward" : "back")} at {(MaxSpeed == null ? "yard Speed" : $"max. speed {(MaxSpeed < 45 ? MaxSpeed + " MPH" : "")}")} and " +
            $"stop {(Before ? "before" : "after")} {GetOrdinal(SwitchCount)} switch.";
    }

    private static string GetOrdinal(int number)
    {
        return number % 100 is >= 11 and <= 13
            ? number + "th"
            : (number % 10) switch
            {
                1 => number + "st",
                2 => number + "nd",
                3 => number + "rd",
                _ => number + "th"
            };
    }

    public override void Execute(BaseLocomotive locomotive)
    {
        if (Before)
        {
            SchedulerPlugin.DebugMessage("Executing order to stop before next switch");
        }

        var persistence = new AutoEngineerPersistence(locomotive.KeyValueObject!);
        var distance = SchedulerUtility.GetDistanceForSwitchOrder(SwitchCount, ClearSwitchesUnderTrain, Before, locomotive, Forward, out _);
        var helper = new AutoEngineerOrdersHelper(locomotive, persistence);
        helper.SetOrdersValue(MaxSpeed == null ? AutoEngineerMode.Yard : AutoEngineerMode.Road, Forward, MaxSpeed, distance);
    }

    public override CustomYieldInstruction WaitBefore()
    {
        return new WaitForSecondsRealtime(4f);
    }

    public override WaitUntil WaitUntilComplete(BaseLocomotive locomotive)
    {
        return new WaitUntil(() => locomotive.IsStopped(1f));
    }

    public override IScheduleCommand Clone()
    {
        return new ScheduleCommandMove(Forward, Before, SwitchCount, MaxSpeed, ClearSwitchesUnderTrain);
    }
}
[Obsolete]
public sealed class ScheduleCommandMoveSerializer : ScheduleCommandSerializerBase<ScheduleCommandMove>
{
    private bool? _Forward;
    private bool? _Before;
    private int? _SwitchCount;
    private int? _MaxSpeed;
    private bool? _ClearSwitchesUnderTrain;

    protected override void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer)
    {
        switch (propertyName)
        {
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

            case "ClearSwitchesUnderTrain":
                _ClearSwitchesUnderTrain = serializer.Deserialize<bool>(reader);
                break;
        }
    }

    protected override ScheduleCommandMove BuildScheduleCommand()
    {
        ThrowIfNull(_Forward, "Forward");
        ThrowIfNull(_Before, "Before");
        ThrowIfNull(_SwitchCount, "SwitchCount");
        ThrowIfNull(_ClearSwitchesUnderTrain, "ClearSwitchesUnderTrain");

        return new ScheduleCommandMove(_Forward!.Value, _Before!.Value, _SwitchCount!.Value, _MaxSpeed, _ClearSwitchesUnderTrain!.Value);
    }

    public override void Write(JsonWriter writer, ScheduleCommandMove value)
    {
        writer.WritePropertyName("Forward");
        writer.WriteValue(value.Forward);

        writer.WritePropertyName("Before");
        writer.WriteValue(value.Before);

        writer.WritePropertyName("SwitchCount");
        writer.WriteValue(value.SwitchCount);

        if (value.MaxSpeed != null)
        {
            writer.WritePropertyName("MaxSpeed");
            writer.WriteValue(value.MaxSpeed.Value);
        }

        writer.WritePropertyName("ClearSwitchesUnderTrain");
        writer.WriteValue(value.ClearSwitchesUnderTrain);
    }
}
[Obsolete]
public sealed class ScheduleCommandMovePanelBuilder : ScheduleCommandPanelBuilderBase
{
    private bool _Direction = true;
    private bool _RoadMode;
    private bool _SwitchLocation;
    private int _MaxSpeed = 15;
    private int _SwitchCount = 1;
    private bool _ClearSwitchesUnderTrain;

    private BaseLocomotive _Locomotive = null!;

    public override void Configure(BaseLocomotive locomotive)
    {
        _Locomotive = locomotive;
    }

    public override void BuildPanel(UIPanelBuilder builder)
    {
        // ReSharper disable once StringLiteralTypo
        builder.AddField("Direction".Color("cccccc")!,
            builder.ButtonStrip(strip =>
            {
                strip.AddButtonSelectable("Forward", _Direction, () => SetToggle(ref _Direction, true));
                strip.AddButtonSelectable("Backward", !_Direction, () => SetToggle(ref _Direction, false));
            })!
        );
        builder.AddField("Mode",
            builder.ButtonStrip(strip =>
            {
                strip.AddButtonSelectable("Road", _RoadMode, () => SetToggle(ref _RoadMode, true));
                strip.AddButtonSelectable("Yard", !_RoadMode, () => SetToggle(ref _RoadMode, false));
            })!
        );
        if (_RoadMode)
        {
            builder.AddField("Max Speed",
                builder.AddSliderQuantized(() => _MaxSpeed,
                    () => _MaxSpeed.ToString("0"),
                    o => _MaxSpeed = (int)o, 5, 0, 45,
                    o => _MaxSpeed = (int)o
                )!
            );
        }

        builder.AddField("Stop Location",
            builder.ButtonStrip(strip =>
            {
                strip.AddButtonSelectable("Before", _SwitchLocation, () => SetToggle(ref _SwitchLocation, true));
                strip.AddButtonSelectable("After", !_SwitchLocation, () => SetToggle(ref _SwitchLocation, false));
            })!
        );
        builder.AddField("Switches",
            builder.ButtonStrip(strip =>
            {
                strip.AddField("Count", $"{_SwitchCount}");
                if (_SwitchCount > 0)
                {
                    strip.AddButton("-1", () =>
                    {
                        --_SwitchCount;
                        strip.Rebuild();
                        MoveCameraToSwitch();
                    });
                }

                strip.AddButton("+1", () =>
                {
                    ++_SwitchCount;
                    strip.Rebuild();
                    MoveCameraToSwitch();
                });
            }
            )!
        );
        builder.AddField("Include under train", builder.AddToggle(() => _ClearSwitchesUnderTrain, o => _ClearSwitchesUnderTrain = o)!);

        return;

        void MoveCameraToSwitch()
        {
            SchedulerUtility.GetDistanceForSwitchOrder(_SwitchCount, _ClearSwitchesUnderTrain, _SwitchLocation, _Locomotive, _Direction, out var targetSwitch);
            if (targetSwitch != null)
            {
                SchedulerUtility.MoveCameraToNode(targetSwitch);
            }
        }

        void SetToggle(ref bool field, bool value)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            builder.Rebuild();
        }
    }

    public override IScheduleCommand CreateScheduleCommand()
    {
        return new ScheduleCommandMove(_Direction, _SwitchLocation, _SwitchCount, _RoadMode ? _MaxSpeed : null, _ClearSwitchesUnderTrain);
    }
}
