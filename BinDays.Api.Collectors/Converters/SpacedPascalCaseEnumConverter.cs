namespace BinDays.Api.Converters;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

/// <summary>
/// Converts a PascalCase enum to and from a space-separated string during JSON serialization.
/// E.g. Enum member "LightBlue" is serialized to "Light Blue".
/// This converter uses a source-generated Regex for optimal performance.
/// </summary>
/// <typeparam name="TEnum">The type of the enum to convert.</typeparam>
public partial class SpacedPascalCaseEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
	// This static partial method will be implemented by the source generator at compile time.
	[GeneratedRegex("(?<!^)([A-Z])", RegexOptions.Compiled)]
	private static partial Regex PascalCaseSplitter();

	/// <summary>
	/// Reads a space-separated string from the JSON and converts it back to its corresponding enum member.
	/// </summary>
	/// <param name="reader">The Utf8JsonReader.</param>
	/// <param name="typeToConvert">The enum type.</param>
	/// <param name="options">The serializer options.</param>
	/// <returns>The deserialized enum member.</returns>
	/// <exception cref="JsonException">Thrown if the JSON value is not a string or cannot be parsed to the enum.</exception>
	public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.String)
		{
			throw new JsonException($"Expected a string to deserialize to enum {typeof(TEnum)} but got {reader.TokenType}.");
		}

		var value = reader.GetString();
		if (string.IsNullOrEmpty(value))
		{
			throw new JsonException("Cannot convert an empty string to an enum.");
		}

		// Convert "Light Blue" back to "LightBlue" to allow parsing
		var enumMemberName = value.Replace(" ", string.Empty);

		if (Enum.TryParse(enumMemberName, ignoreCase: true, out TEnum result))
		{
			return result;
		}

		throw new JsonException($"Unable to convert '{value}' to enum {typeof(TEnum)}.");
	}

	/// <summary>
	/// Writes an enum member to the JSON as a space-separated string.
	/// </summary>
	/// <param name="writer">The Utf8JsonWriter.</param>
	/// <param name="value">The enum member to write.</param>
	/// <param name="options">The serializer options.</param>
	public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
	{
		// Convert "LightBlue" to "Light Blue" using the source-generated Regex
		var pascalCaseString = value.ToString();
		var spacedString = PascalCaseSplitter().Replace(pascalCaseString, " $1");
		writer.WriteStringValue(spacedString);
	}
}
