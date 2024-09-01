# Train command scheduler

The player has the option to create a schedule, and later tell the AI engineer to execute it.

# Installation

-   Download Scheduler-VERSION.zip from the releases page
-   Install with [Railloader](<[https://www.nexusmods.com/site/mods/21](https://railroader.stelltis.ch/)>)

# List of currently implemented Commands:



-   [Connect Air](#user-content-connect-air)
-   [Move](#user-content-move)
-   [Notice Wait](#user-content-notice-wait)
-   [Release handbrakes](#user-content-release-handbrakes)
-   [Restore switches](#user-content-restore-switches)
-   [Set handbrake](#user-content-set-handbrake)
-   [Set switch](#user-content-set-switch)
-   [Uncouple](#user-content-uncouple)
-   [Wait](#user-content-wit)

## Connect Air

Connect air on train.

## Move

Move train

| Parameter            | Description                         |
| -------------------- | ----------------------------------- |
| **Direction**        | Move direction (forward / backward) |
| **AutoEngineerMode** | Yard / Road mode                    |
| **MaxRoadSpeed**     | Max. speed when in road mode        |
| **StopMode**         | Stop behavior                       |

### Stop modes

-   **CarLengths**: Train will travel N car lengths before stopping.

## Notice Wait

Shows notice to player and wait until player dismiss it.

| Parameter   | Description                              |
| ----------- | ---------------------------------------- |
| **Message** | Message shown as notification to player. |

## Release handbrakes

Release all handbrakes on train.

## Restore switches

Restore state of switches, that where thrown by this schedule.

## Set handbrake

Sets handbrake on given car. Car index counted from locomotive.

| Parameter    | Description                        |
| ------------ | ---------------------------------- |
| **CarIndex** | Car index counted from locomotive. |

## Set switch

Save current switch state and set desired state.

| Parameter    | Description                                           |
| ------------ | ----------------------------------------------------- |
| **Id**       | Node id of target switch.                             |
| **IsThrown** | Desired state of switch (true=reversed, false=normal) |

## Uncouple

Uncouple given car. Car index counted from locomotive.

| Parameter    | Description                        |
| ------------ | ---------------------------------- |
| **CarIndex** | Car index counted from locomotive. |

## Wait

Wait given amount of milliseconds before continuing with next command in schedule.

| Parameter        | Description                     |
| ---------------- | ------------------------------- |
| **MilliSeconds** | Number of milliseconds to wait. |

# Project Setup

In order to get going with this, follow the following steps:

1. Clone the repo
2. Copy the `Paths.user.example` to `Paths.user`, open the new `Paths.user` and set the `<GameDir>` to your game's directory.
3. Open the Solution
4. You're ready!

## During Development

Make sure you're using the _Debug_ configuration. Every time you build your project, the files will be copied to your Mods folder and you can immediately start the game to test it.

## Publishing

Make sure you're using the _Release_ configuration. The build pipeline will then automatically do a few things:

1. Makes sure it's a proper release build without debug symbols
1. Replaces `$(AssemblyVersion)` in the `Definition.json` with the actual assembly version.
1. Copies all build outputs into a zip file inside `bin` with a ready-to-extract structure inside, named like the project they belonged to and the version of it.

# Custom commands

You can implement your own command by implementing `ICommand` interface, inheriting `CommandManager<TCommand>` class and registering them in `ScheduleCommands`.

Implementation of 'Wait' command:

```cs
public sealed class Wait(float milliSeconds) : ICommand
{
    // Text shown in Scheduler dialog
    public string DisplayText => $"Wait for {MilliSeconds * 0.001f:0.###} seconds";

    // Command parameter
    public float MilliSeconds { get; } = milliSeconds;
}

public sealed class WaitManager : CommandManager<Wait>
{
    // Execute this command
    // state holds data, that can be pass between commands
    // "schedule"       Schedule          Reference to entire schedule
    // "locomotive"     BaseLocomitive    Reference to locomotive
    // "wage"           int               Wage to engineer after schedule complated:
    // "index"          int               Command index in schedule
    // "stop"           bool?             If set to true, schedule execution will be aborted
    public override IEnumerator Execute(Dictionary<string, object> state) {
        return new WaitForSecondsRealtime(Command!.MilliSeconds);
    }

    private float? _MilliSeconds;

    // Serialize command parameter(s) to json
    public override void SerializeProperties(JsonWriter writer) {
        writer.WritePropertyName(nameof(Wait.MilliSeconds));
        writer.WriteValue(Command!.MilliSeconds);
    }

    // Read json property to temporary field
    protected override void ReadProperty(string propertyName, JsonReader reader, JsonSerializer serializer) {
        if (propertyName == nameof(Wait.MilliSeconds)) {
            _MilliSeconds = serializer.Deserialize<float>(reader);
        }
    }

    // Verify, that mandatory properties where loaded via ReadProperty
    // and create command
    protected override object TryCreateCommand() {
        if (_MilliSeconds == null) {
            return "Missing mandatory property 'MilliSeconds'.";
        }

        return new Wait(_MilliSeconds!.Value);
    }

    // UI used in Schedule dialog when player configure command
    public override void BuildPanel(UIPanelBuilder builder, BaseLocomotive locomotive) {
        builder.AddField("Milliseconds", builder.AddSlider(() => _MilliSeconds ?? 0, () => (_MilliSeconds ?? 0).ToString("0"), o => _MilliSeconds = o, 0, 60 * 60 * 1000, true, o => _MilliSeconds = o)!);
    }
}

// Command registration during plugin initialziation:
ScheduleCommands.Register<Wait, WaitManager>();
```
