using System.Collections;
using System.Collections.Generic;
using Model;
using Newtonsoft.Json;
using Serilog;
using UI.Builder;

namespace Scheduler.Utility;

public abstract class CommandManager
{
    protected readonly ILogger Logger = Log.ForContext(typeof(CommandManager))!;

    public ICommand? Command { get; set; }

    public virtual void BuildPanel(UIPanelBuilder builder, BaseLocomotive locomotive) {
    }

    public virtual IEnumerator Execute(Dictionary<string, object> state) {
        Logger.Information($"Execute: {Command.GetType().Name}");
        yield break;
    }

    protected virtual void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer) {
    }

    public virtual void Serialize(JsonWriter writer) {
        Logger.Information($"Serialize: {Command.GetType().Name}");
    }

    public void Deserialize(JsonReader reader, JsonSerializer serializer) {
        Logger.Information("Deserialize");
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
        Logger.Information($"Deserialized:  {Command.GetType().Name}");
    }

    public abstract ICommand CreateCommand();

    protected void ThrowIfNull(object? value, string propertyName) {
        if (value == null) {
            throw new JsonSerializationException($"Missing mandatory property '{propertyName}'.");
        }
    }
}

public abstract class CommandManager<TCommand> : CommandManager where TCommand : ICommand
{
    public new TCommand? Command {
        get => (TCommand?)base.Command;
        set => base.Command = value;
    }
}
