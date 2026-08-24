using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeveloperMemory.Api.Models;

/// <summary>
/// Handles OpenAI's "content" field which can be either:
/// - A plain string: "content": "Hello"
/// - An array of content parts: "content": [{"type": "text", "text": "..."}, ...]
/// 
/// On read: converts both to a JSON string stored in Message.Content
/// On write: serializes as-is (preserves the original format for forwarding).
/// </summary>
public class MessageContentConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            // Read the entire array and serialize it back to a JSON string
            using var doc = JsonDocument.ParseValue(ref reader);
            return doc.RootElement.GetRawText();
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        // For any other token type, try to get raw text
        using var fallbackDoc = JsonDocument.ParseValue(ref reader);
        return fallbackDoc.RootElement.GetRawText();
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        // If the string looks like a JSON array, write it as raw JSON
        var trimmed = value.AsSpan().Trim();
        if ((trimmed.Length > 0 && trimmed[0] == '[') ||
            (trimmed.Length > 0 && trimmed[0] == '{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(value);
                doc.RootElement.WriteTo(writer);
                return;
            }
            catch (JsonException)
            {
                // Not valid JSON, fall through to write as string
            }
        }

        writer.WriteStringValue(value);
    }
}
