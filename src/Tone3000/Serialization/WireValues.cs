using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tone3000.Serialization;

/// <summary>
/// Marks the exact string the TONE3000 API uses for an enum member.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class WireValueAttribute(string value, bool deprecated = false) : Attribute
{
    /// <summary>The wire value.</summary>
    public string Value { get; } = value;

    /// <summary>True when the API still accepts this value but has replaced it.</summary>
    public bool Deprecated { get; } = deprecated;
}

/// <summary>
/// Converts between enum members and their TONE3000 wire values.
/// Unrecognised values read as the enum's default member (<c>Unknown</c>), so a new value
/// added by the API never breaks deserialization.
/// </summary>
public static class WireValues
{
    /// <summary>Returns the wire value for <paramref name="value"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value has no wire form (for example <c>Unknown</c>).</exception>
    public static string ToWire<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(TEnum value)
        where TEnum : struct, Enum
        => Map<TEnum>.ToWire.TryGetValue(value, out var wire)
            ? wire
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"{typeof(TEnum).Name}.{value} has no TONE3000 wire value.");

    /// <summary>Parses a wire value, returning the default (<c>Unknown</c>) member when it is not recognised.</summary>
    public static TEnum FromWire<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(string? wire)
        where TEnum : struct, Enum
        => wire is not null && Map<TEnum>.FromWire.TryGetValue(wire, out var value) ? value : default;

    private static class Map<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>
        where TEnum : struct, Enum
    {
        public static readonly FrozenDictionary<TEnum, string> ToWire;
        public static readonly FrozenDictionary<string, TEnum> FromWire;

        static Map()
        {
            var to = new Dictionary<TEnum, string>();
            var from = new Dictionary<string, TEnum>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetCustomAttribute<WireValueAttribute>() is not { } attr)
                {
                    continue;
                }

                var value = (TEnum)field.GetValue(null)!;
                to[value] = attr.Value;
                from[attr.Value] = value;
            }

            ToWire = to.ToFrozenDictionary();
            FromWire = from.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }
    }
}

/// <summary>System.Text.Json converter that uses <see cref="WireValueAttribute"/> values.</summary>
public sealed class WireEnumConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    /// <inheritdoc />
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.String => WireValues.FromWire<TEnum>(reader.GetString()),
            // Architecture values ("1", "2") could plausibly arrive as JSON numbers.
            JsonTokenType.Number => WireValues.FromWire<TEnum>(reader.TryGetInt64(out var n) ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : null),
            JsonTokenType.Null => default,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for {typeof(TEnum).Name}."),
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        => writer.WriteStringValue(WireValues.ToWire(value));
}
