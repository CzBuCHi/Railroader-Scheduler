using System.Collections;
using System.Collections.Generic;
using Model;
using Newtonsoft.Json;
using UI.Builder;

namespace Scheduler.Utility;

public abstract class CommandManager
{
    public virtual void BuildPanel(UIPanelBuilder builder, BaseLocomotive locomotive) {
    }

    public abstract ICommand CreateCommand();
}

public abstract class CommandManager<TCommand> : CommandManager where TCommand : ICommand
{
    public TCommand? Command { get; set; }

    public abstract IEnumerator Execute(Dictionary<string, object> state);

    public virtual void Serialize(JsonWriter writer) {
    }

    public void Deserialize(JsonReader reader, JsonSerializer serializer) {
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

        Command = CreateCommandBase();
    }

    protected virtual void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer) {
    }

    protected void ThrowIfNull(object? value, string propertyName) {
        if (value == null) {
            throw new JsonSerializationException($"Missing mandatory property '{propertyName}'.");
        }
    }

    public override ICommand CreateCommand() {
        return CreateCommandBase();
    }

    protected abstract TCommand CreateCommandBase();
}
