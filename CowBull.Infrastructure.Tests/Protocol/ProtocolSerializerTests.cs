using System.Text.Json;
using CowBull.Infrastructure.Protocol;

namespace CowBull.Infrastructure.Tests.Protocol;

public sealed class ProtocolSerializerTests
{
    [Fact]
    public void SerializeAndDeserialize_RoundTripsEveryProtocolMessage()
    {
        Guid sessionId = Guid.NewGuid();
        ProtocolMessage[] messages =
        {
            new NewGameRequest(Guid.NewGuid(), 4, 10),
            new NewGameResponse(Guid.NewGuid(), sessionId, 4, 10),
            new GuessRequest(Guid.NewGuid(), sessionId, "1234"),
            new GuessResponse(Guid.NewGuid(), sessionId, "1234", 1, 2, 3, false, false),
            new SurrenderRequest(Guid.NewGuid(), sessionId),
            new GameEndedResponse(Guid.NewGuid(), sessionId, GameEndReason.Surrendered, "9876", 3),
            new GameEndedResponse(Guid.NewGuid(), sessionId, GameEndReason.TimedOut, "9876", 3),
            new ErrorResponse(Guid.NewGuid(), sessionId, "invalidGuess", "The guess is invalid."),
        };

        foreach (ProtocolMessage expected in messages)
        {
            string json = ProtocolSerializer.Serialize(expected);
            ProtocolMessage actual = ProtocolSerializer.Deserialize(json);

            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Serialize_UsesCamelCaseDiscriminatorsEnumsAndProperties()
    {
        var response = new GameEndedResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            GameEndReason.AttemptsExhausted,
            "9876",
            10);

        string json = ProtocolSerializer.Serialize(response);

        Assert.Contains("\"type\":\"gameEnded\"", json, StringComparison.Ordinal);
        Assert.Contains("\"messageId\":", json, StringComparison.Ordinal);
        Assert.Contains("\"reason\":\"attemptsExhausted\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"MessageId\":", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GuessResponse_HasNoSecretThatCouldDiscloseAnActiveGame()
    {
        var response = new GuessResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "1234",
            0,
            2,
            1,
            false,
            false);

        string json = ProtocolSerializer.Serialize(response);

        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("9876", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_RejectsUnknownPropertiesAndWrongPropertyCasing()
    {
        Guid messageId = Guid.NewGuid();
        string json = "{\"type\":\"newGame\",\"MessageId\":\"" + messageId + "\",\"numberLength\":4,\"maximumAttempts\":10}";

        Assert.Throws<JsonException>(() => ProtocolSerializer.Deserialize(json));
    }
}
