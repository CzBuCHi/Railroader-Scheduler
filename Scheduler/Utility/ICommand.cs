using System;
using Newtonsoft.Json;
using Scheduler.Commands;

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
        writer.WriteValue(value.GetType().FullName);
        var manager = ScheduleCommands.GetManager(value.GetType());
        if (manager == null) {
            throw new JsonSerializationException($"Serializer for '{value.GetType().Name}' was not found.");
        }

        manager.Command = value;
        manager.SerializeProperties(writer);
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

        var manager = ScheduleCommands.GetManager(type);
        if (manager == null) {
            throw new JsonSerializationException($"Serializer for '{typeName}' was not found.");
        }

        try {
            manager.Deserialize(reader, serializer);
            return manager.Command;
        } catch (Exception e) {
            return new DeserializationFailed("Deserialization Failed: " + e.Message);
        }
  
    }
}
