using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trakt.Api.Converters;

/// <summary>
/// Reads a nullable <see cref="bool"/> that trakt.tv may send as a boolean, a number or a string.
/// </summary>
/// <remarks>
/// The collection metadata 3d flag is null for almost every item but comes back as the number 1 for
/// some. An unrecognized value degrades to null rather than throwing, so one oddly typed item does
/// not abort the sync.
/// </remarks>
public class TraktFlexibleBooleanConverter : JsonConverter<bool?>
{
    /// <inheritdoc />
    public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.Number:
                return reader.TryGetDouble(out var number) ? number != 0 : null;
            case JsonTokenType.String:
                var text = reader.GetString();
                if (bool.TryParse(text, out var parsedBoolean))
                {
                    return parsedBoolean;
                }

                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedNumber)
                    ? parsedNumber != 0
                    : null;
            case JsonTokenType.Null:
                return null;
            default:
                reader.Skip();
                return null;
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value.HasValue)
        {
            writer.WriteBooleanValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
