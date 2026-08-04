using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace CowBull.Infrastructure.Protocol;

public static class ProtocolSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    public static string Serialize(ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        message.Validate();
        return JsonSerializer.Serialize(message, SerializerOptions);
    }

    public static byte[] SerializeToUtf8Bytes(ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        message.Validate();
        return JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
    }

    public static ProtocolMessage Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        ProtocolMessage message = JsonSerializer.Deserialize<ProtocolMessage>(json, SerializerOptions)
            ?? throw new JsonException("The protocol message cannot be null.");
        message.Validate();
        return message;
    }

    public static ProtocolMessage Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.IsEmpty)
        {
            throw new ArgumentException("The JSON payload cannot be empty.", nameof(utf8Json));
        }

        ProtocolMessage message = JsonSerializer.Deserialize<ProtocolMessage>(utf8Json, SerializerOptions)
            ?? throw new JsonException("The protocol message cannot be null.");
        message.Validate();
        return message;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = false,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.MakeReadOnly();
        return options;
    }
}
