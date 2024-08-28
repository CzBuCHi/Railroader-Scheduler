using System;
using System.Collections;
using System.Collections.Generic;
using Model;
using Newtonsoft.Json;
using Scheduler.Commands;
using Scheduler.UI;
using Serilog;
using UI.Builder;

namespace Scheduler.Utility;

/// <summary> Base class used to manage command. </summary>
public abstract class CommandManager
{
    protected readonly ILogger Logger;

    protected CommandManager() {
        Logger = Log.ForContext(GetType())!;
    }

    /// <summary> Instance of command to manage. </summary>
    public ICommand? Command { get; set; }

    /// <summary> When set and this command type is selected in <see cref="SchedulerDialog"/> game will show visualizers on every switch (used to select switch). </summary>
    public virtual bool ShowTrackSwitchVisualizers => false;

    /// <summary> Build panel, that is placed inside <see cref="SchedulerDialog"/> when  this command type is selected. </summary>
    /// <param name="builder">Panel builder.</param>
    /// <param name="locomotive">Current locomotive.</param>
    public virtual void BuildPanel(UIPanelBuilder builder, BaseLocomotive locomotive) {
    }

    /// <summary> Execute current command. </summary>
    /// <param name="state">State used to pass values between commands.</param>
    /// <returns>Enumerator, that is processed by unity Coroutine.</returns>
    public abstract IEnumerator Execute(Dictionary<string, object> state);
    
    /// <summary> Read single property value from json <paramref name="reader"/>. </summary>
    /// <param name="propertyName">Name of property to read.</param>
    /// <param name="reader">Json reader.</param>
    /// <param name="serializer">Json serializer.</param>
    protected virtual void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer) {
    }

    /// <summary> Serialize properties of current command to <paramref name="writer"/>. </summary>
    /// <param name="writer">Json writer.</param>
    public virtual void SerializeProperties(JsonWriter writer) {
    }

    internal void Deserialize(JsonReader reader, JsonSerializer serializer) {
        while (reader.Read()) {
            if (reader.TokenType == JsonToken.PropertyName) {
                var propertyName = (string?)reader.Value;
                reader.Read();
                ReadProperty(propertyName, reader, serializer);
            }

            if (reader.TokenType == JsonToken.EndObject) {
                break;
            }
        }

        Command = CreateCommand();
    }

    /// <summary> Create command instance. </summary>
    /// <returns>NEw instance of command.</returns>
    public abstract ICommand CreateCommand();

    /// <summary>
    /// Throws <see cref="JsonSerializationException"/> if <paramref name="value"/> is null.
    /// </summary>
    /// <param name="value">Value to check.</param>
    /// <param name="propertyName">Associated property.</param>
    /// <exception cref="JsonSerializationException"></exception>
    protected void ThrowIfNull(object? value, string propertyName) {
        if (value == null) {
            throw new JsonSerializationException($"Missing mandatory property '{propertyName}'.");
        }
    }
}

/// <summary> Base class used to manage command. </summary>
/// <typeparam name="TCommand">Command type.</typeparam>
public abstract class CommandManager<TCommand> : CommandManager where TCommand : ICommand
{
    public new TCommand? Command {
        get => (TCommand?)base.Command;
        set => base.Command = value;
    }

    public override ICommand CreateCommand() {
        var result = TryCreateCommand();
        if (result is ICommand command) {
            return command;
        }

        var errorMessage = $"Failed to load command {typeof(TCommand).Name} from json.";
        Logger.Error($"{errorMessage} {result}");
        return new DeserializationFailed(errorMessage);
    }

    protected abstract object TryCreateCommand();
}
