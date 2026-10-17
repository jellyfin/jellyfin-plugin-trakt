using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trakt.Api.Converters;

/// <summary>
/// Reads a nullable enum that trakt.tv sends as a string, turning values the enum does not know into null.
/// </summary>
/// <typeparam name="TEnum">The enum type.</typeparam>
/// <remarks>
/// <see cref="JsonStringEnumConverter"/> throws on a string that is not a member name, so one value
/// trakt.tv adds later aborts reading the whole response, such as the user's collection. Member names
/// still match ignoring case and surrounding spaces, as they do there; any other string, number or
/// token degrades to null. Writing uses the enum converter from the serializer options, so outgoing
/// payloads are unchanged.
/// </remarks>
public class TraktTolerantEnumConverter<TEnum> : JsonConverter<TEnum?>
    where TEnum : struct, Enum
{
    /// <inheritdoc />
    public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                var text = reader.GetString().Trim(' ');
                foreach (var value in Enum.GetValues<TEnum>())
                {
                    if (string.Equals(value.ToString(), text, StringComparison.OrdinalIgnoreCase))
                    {
                        return value;
                    }
                }

                return null;
            case JsonTokenType.Null:
                return null;
            default:
                // Skip throws on a partial buffer; the serializer has already buffered the whole value, so TrySkip succeeds
                reader.TrySkip();
                return null;
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TEnum? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value.HasValue)
        {
            JsonSerializer.Serialize(writer, value.Value, options);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
