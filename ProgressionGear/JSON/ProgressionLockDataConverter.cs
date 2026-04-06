using ProgressionGear.ProgressionLock;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProgressionGear.JSON
{
    public sealed class ProgressionLockDataConverter : JsonConverter<ProgressionLockData>
    {
        public override ProgressionLockData? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            ProgressionLockData data = new();

            if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Expected progression lock to be an object");

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return data;

                if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Expected PropertyName token");

                string property = reader.GetString()!;
                reader.Read();
                switch (property.ToLowerInvariant().Replace(" ", ""))
                {
                    case "unlocklayoutids":
                    case "unlocktiers":
                    case "unlock":
                        if (PWJson.TryDeserialize<List<ProgressionRequirement>>(ref reader, out var unlocks, options))
                            data.Unlock = unlocks;
                        break;
                    case "unlockrequired":
                        if (reader.TokenType != JsonTokenType.Number) throw new JsonException("Expected number for UnlockRequired");
                        data.UnlockRequired = reader.GetInt32();
                        break;
                    case "locklayoutids":
                    case "locktiers":
                    case "lock":
                        if (PWJson.TryDeserialize<List<ProgressionRequirement>>(ref reader, out var locks, options))
                            data.Lock = locks;
                        break;
                    case "lockrequired":
                        if (reader.TokenType != JsonTokenType.Number) throw new JsonException("Expected number for LockRequired");
                        data.LockRequired = reader.GetInt32();
                        break;
                    case "missingleveldefault":
                        if (reader.TokenType != JsonTokenType.True && reader.TokenType != JsonTokenType.False) throw new JsonException("Expected boolean for MissingLevelDefault");
                        data.MissingLevelDefault = reader.GetBoolean();
                        break;
                    case "offlineids":
                        if (PWJson.TryDeserialize<List<uint>>(ref reader, out var ids, options))
                            data.OfflineIDs = ids;
                        break;
                    case "priority":
                        if (reader.TokenType != JsonTokenType.Number) throw new JsonException("Expected number for Priority");
                        data.Priority = reader.GetInt32();
                        break;
                    case "overload":
                        if (reader.TokenType != JsonTokenType.String) throw new JsonException("Expected string for Name");
                        data.Name = reader.GetString()!;
                        break;
                }
            }

            throw new JsonException("Expected EndObject token");
        }

        public override void Write(Utf8JsonWriter writer, ProgressionLockData? value, JsonSerializerOptions options)
        {
            if (value == null) return;

            writer.WriteStartObject();
            writer.WritePropertyName(nameof(value.Unlock));
            PWJson.Serialize(writer, value.Unlock);
            writer.WriteNumber(nameof(value.UnlockRequired), value.UnlockRequired);
            writer.WritePropertyName(nameof(value.Lock));
            PWJson.Serialize(writer, value.Lock);
            writer.WriteNumber(nameof(value.LockRequired), value.LockRequired);
            writer.WritePropertyName(nameof(value.OfflineIDs));
            PWJson.Serialize(writer, value.OfflineIDs);
            writer.WriteNumber(nameof(value.Priority), value.Priority);
            writer.WriteString(nameof(value.Name), value.Name);
            writer.WriteEndObject();
        }
    }
}
