using ProgressionGear.ProgressionLock;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProgressionGear.JSON
{
    public sealed class GearToggleDataConverter : JsonConverter<GearToggleData>
    {
        public override GearToggleData? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            GearToggleData data = new();

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
                    case "offlineids":
                        if (PWJson.TryDeserialize<List<uint>>(ref reader, out var ids, options))
                            data.OfflineIDs = ids;
                        else
                            throw new JsonException("GearToggle: Failed to read OfflineIDs!");
                        break;
                    case "buttontext":
                        if (TryReadButtonText(ref reader, out var buttonText, options))
                            data.ButtonText = buttonText;
                        else
                            throw new JsonException("GearToggle: Failed to read ButtonText!");
                        break;
                    case "reverseorder":
                        if (reader.TokenType != JsonTokenType.True && reader.TokenType != JsonTokenType.False) throw new JsonException("GearToggle: Expected boolean for ReverseOrder");
                        data.ReverseOrder = reader.GetBoolean();
                        break;
                    case "name":
                        if (reader.TokenType != JsonTokenType.String) throw new JsonException("GearToggle: Expected string for Name");
                        data.Name = reader.GetString()!;
                        break;
                }
            }

            throw new JsonException("Expected EndObject token");
        }

        private static bool TryReadButtonText(ref Utf8JsonReader reader, out Localization.LocalizedText[] buttonText, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                buttonText = new Localization.LocalizedText[2];
                reader.Read();
                if (!PWJson.TryDeserialize(ref reader, out buttonText[1]!, options))
                    throw new JsonException($"GearToggle: Failed to read right ButtonText!");

                reader.Read();
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    buttonText = new Localization.LocalizedText[] { buttonText[1] };
                    return true;
                }
                else if (reader.TokenType == JsonTokenType.Null)
                {
                    buttonText = new Localization.LocalizedText[] { buttonText[1] };
                    reader.Read();
                }
                else if (!PWJson.TryDeserialize(ref reader, out buttonText[0]!, options))
                    throw new JsonException($"GearToggle: Failed to read left ButtonText!");

                reader.Read();
                if (reader.TokenType != JsonTokenType.EndArray)
                    throw new JsonException($"GearToggle: Expected EndArray token when reading ButtonText, found {reader.TokenType}");
                return true;
            }
            else
            {
                buttonText = new Localization.LocalizedText[1];
                if (!PWJson.TryDeserialize(ref reader, out buttonText[0]!, options))
                    throw new JsonException($"GearToggle: Failed to read ButtonText!");
            }

            return true;
        }

        public override void Write(Utf8JsonWriter writer, GearToggleData? value, JsonSerializerOptions options)
        {
            if (value == null) return;

            writer.WriteStartObject();
            PWJson.Serialize(writer, nameof(value.OfflineIDs), value.OfflineIDs, options);
            if (value.ButtonText.Length > 1)
            {
                writer.WriteStartArray(nameof(value.ButtonText));
                PWJson.Serialize(writer, value.ButtonText[0], options);
                PWJson.Serialize(writer, value.ButtonText[1], options);
                writer.WriteEndArray();
            }
            else
                PWJson.Serialize(writer, nameof(value.ButtonText), value.ButtonText[0], options);
            writer.WriteBoolean(nameof(value.ReverseOrder), value.ReverseOrder);
            writer.WriteString(nameof(value.Name), value.Name);
            writer.WriteEndObject();
        }
    }
}
