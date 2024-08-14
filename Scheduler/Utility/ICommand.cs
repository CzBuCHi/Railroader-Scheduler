using System;
using Newtonsoft.Json;

namespace Scheduler.Utility;

[JsonConverter(typeof(CommandConverter))]
public interface ICommand
{
    string DisplayText { get; }
}

internal sealed class CommandConverter : JsonConverter<ICommand>
{
    public override void WriteJson(JsonWriter writer, ICommand? value, JsonSerializer serializer) {
        if (value == null) {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName("$Type");
        // TODO: AssemblyQualifiedName ?
        writer.WriteValue(value.GetType().Name);
        var manager = ScheduleCommands.GetManager(value);
        if (manager == null) {
            throw new JsonSerializationException($"Serializer for '{value.GetType().Name}' was not found.");
        }

        manager.Serialize(writer);
        writer.WriteEndObject();
    }

    public override ICommand? ReadJson(JsonReader reader, Type objectType, ICommand? existingValue, bool hasExistingValue, JsonSerializer serializer) {
        if (reader.TokenType != JsonToken.StartObject) {
            return null;
        }

        reader.Read(); // Move to the first property
        if (reader.TokenType != JsonToken.PropertyName || (string?)reader.Value != "$Type") {
            throw new JsonSerializationException("Expected $Type property.");
        }

        reader.Read(); // Move to the type value
        var typeName = reader.Value as string;
        if (typeName == null) {
            throw new JsonSerializationException("Expected type name as string.");
        }

        var type = Type.GetType(typeName);
        if (type == null) {
            throw new JsonSerializationException($"Cannot resolve command from '{typeName}'.");
        }

        var manager = ScheduleCommands.GetManager(existingValue!);
        if (manager == null) {
            throw new JsonSerializationException($"Serializer for '{typeName}' was not found.");
        }

        manager.Deserialize(reader, serializer);
        return manager.Command;
    }
}
