namespace BinDays.Api.Collectors.Converters;

using BinDays.Api.Collectors.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Serializes <see cref="BinColour"/> as its <see cref="BinColour.Name"/> string
/// for backwards compatibility with existing clients.
/// </summary>
public class BinColourJsonConverter : JsonConverter<BinColour>
{
	/// <inheritdoc/>
	public override BinColour Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.String)
		{
			throw new JsonException($"Expected a string to deserialize to BinColour but got {reader.TokenType}.");
		}

		var value = reader.GetString();
		if (string.IsNullOrEmpty(value))
		{
			throw new JsonException("Cannot convert an empty string to a BinColour.");
		}

		// Return a BinColour with the name but no hex (for deserialization from legacy format).
		return new BinColour(value, string.Empty);
	}

	/// <inheritdoc/>
	public override void Write(Utf8JsonWriter writer, BinColour value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value.Name);
	}
}
