namespace Scheduler.Data;

using System;
using Newtonsoft.Json;

/// <summary> Command json serializer. </summary>
public interface IScheduleCommandSerializer {

    IScheduleCommand ReadCore(JsonReader reader, JsonSerializer serializer);
    void WriteCore(JsonWriter writer, IScheduleCommand value);

}

public interface IScheduleCommandSerializer<TScheduleCommand> : IScheduleCommandSerializer
    where TScheduleCommand : IScheduleCommand {

    TScheduleCommand Read(JsonReader reader, JsonSerializer serializer);
    void Write(JsonWriter writer, TScheduleCommand value);

}

public abstract class ScheduleCommandSerializerBase<TScheduleCommand> : IScheduleCommandSerializer<TScheduleCommand> where TScheduleCommand : IScheduleCommand {

    IScheduleCommand IScheduleCommandSerializer.ReadCore(JsonReader reader, JsonSerializer serializer) {
        return Read(reader, serializer);
    }

    void IScheduleCommandSerializer.WriteCore(JsonWriter writer, IScheduleCommand value) {
        Write(writer, (TScheduleCommand)value);
    }

    public TScheduleCommand Read(JsonReader reader, JsonSerializer serializer) {
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

        return BuildScheduleCommand();
    }

    public virtual void Write(JsonWriter writer, TScheduleCommand value) {
    }

    protected void ThrowIfNull(object? value, string propertyName) {
        if (value == null) {
            throw new JsonSerializationException($"Missing mandatory property '{propertyName}'.");
        }
    }

    protected virtual void ReadProperty(string? propertyName, JsonReader reader, JsonSerializer serializer) {
    }

    protected virtual TScheduleCommand BuildScheduleCommand() {
        return Activator.CreateInstance<TScheduleCommand>()!;
    }

}