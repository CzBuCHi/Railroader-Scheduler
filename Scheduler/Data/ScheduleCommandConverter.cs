namespace Scheduler.Data;

using System;
using Newtonsoft.Json;

internal sealed class ScheduleCommandConverter : JsonConverter<IScheduleCommand> {

    public override void WriteJson(JsonWriter writer, IScheduleCommand? value, JsonSerializer serializer) {
        if (value == null) {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName("$Type");
        writer.WriteValue(value.Identifier);
        var itemSerializer = ScheduleCommands.FindSerializer(value.Identifier);
        if (itemSerializer == null) {
            throw new JsonSerializationException($"Serializer for '{value.Identifier}' was not found.");
        }

        itemSerializer.WriteCore(writer, value);
        writer.WriteEndObject();
    }

    public override IScheduleCommand? ReadJson(JsonReader reader, Type objectType, IScheduleCommand? existingValue, bool hasExistingValue, JsonSerializer serializer) {
        if (reader.TokenType != JsonToken.StartObject) {
            return null;
        }

        reader.Read(); // Move to the first property
        if (reader.TokenType != JsonToken.PropertyName || (string?)reader.Value != "$Type") {
            throw new JsonSerializationException("Expected $Type property.");
        }

        reader.Read(); // Move to the type value
        var identifier = reader.Value as string;
        if (identifier == null) {
            throw new JsonSerializationException("Expected type name as string.");
        }

        var itemSerializer = ScheduleCommands.FindSerializer(identifier);
        if (itemSerializer == null) {
            throw new JsonSerializationException($"Serializer for '{identifier}' was not found.");
        }

        return itemSerializer.ReadCore(reader, serializer);
    }

}