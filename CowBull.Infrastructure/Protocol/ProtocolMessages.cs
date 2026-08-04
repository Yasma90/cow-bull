using System.Text.Json.Serialization;

namespace CowBull.Infrastructure.Protocol;

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(NewGameRequest), "newGame")]
[JsonDerivedType(typeof(NewGameResponse), "newGameResponse")]
[JsonDerivedType(typeof(GuessRequest), "guess")]
[JsonDerivedType(typeof(GuessResponse), "guessResponse")]
[JsonDerivedType(typeof(SurrenderRequest), "surrender")]
[JsonDerivedType(typeof(GameEndedResponse), "gameEnded")]
[JsonDerivedType(typeof(ErrorResponse), "error")]
public abstract record ProtocolMessage
{
    protected ProtocolMessage(Guid messageId)
    {
        MessageId = ProtocolValidation.RequiredId(messageId, nameof(messageId));
    }

    public Guid MessageId { get; }

    internal virtual void Validate()
    {
        _ = ProtocolValidation.RequiredId(MessageId, nameof(MessageId));
    }
}

public sealed record NewGameRequest : ProtocolMessage
{
    [JsonConstructor]
    public NewGameRequest(Guid messageId, int numberLength = 4, int maximumAttempts = 10)
        : base(messageId)
    {
        NumberLength = ProtocolValidation.InRange(numberLength, 1, 10, nameof(numberLength));
        MaximumAttempts = ProtocolValidation.InRange(maximumAttempts, 1, 1_000, nameof(maximumAttempts));
    }

    public int NumberLength { get; }

    public int MaximumAttempts { get; }

    internal override void Validate()
    {
        base.Validate();
        _ = ProtocolValidation.InRange(NumberLength, 1, 10, nameof(NumberLength));
        _ = ProtocolValidation.InRange(MaximumAttempts, 1, 1_000, nameof(MaximumAttempts));
    }
}

public sealed record NewGameResponse : ProtocolMessage
{
    [JsonConstructor]
    public NewGameResponse(Guid messageId, Guid sessionId, int numberLength, int maximumAttempts)
        : base(messageId)
    {
        SessionId = ProtocolValidation.RequiredId(sessionId, nameof(sessionId));
        NumberLength = ProtocolValidation.InRange(numberLength, 1, 10, nameof(numberLength));
        MaximumAttempts = ProtocolValidation.InRange(maximumAttempts, 1, 1_000, nameof(maximumAttempts));
    }

    public Guid SessionId { get; }

    public int NumberLength { get; }

    public int MaximumAttempts { get; }

    internal override void Validate()
    {
        base.Validate();
        _ = ProtocolValidation.RequiredId(SessionId, nameof(SessionId));
        _ = ProtocolValidation.InRange(NumberLength, 1, 10, nameof(NumberLength));
        _ = ProtocolValidation.InRange(MaximumAttempts, 1, 1_000, nameof(MaximumAttempts));
    }
}

public sealed record GuessRequest : ProtocolMessage
{
    [JsonConstructor]
    public GuessRequest(Guid messageId, Guid sessionId, string guess)
        : base(messageId)
    {
        SessionId = ProtocolValidation.RequiredId(sessionId, nameof(sessionId));
        Guess = ProtocolValidation.Guess(guess, nameof(guess));
    }

    public Guid SessionId { get; }

    public string Guess { get; }

    internal override void Validate()
    {
        base.Validate();
        _ = ProtocolValidation.RequiredId(SessionId, nameof(SessionId));
        _ = ProtocolValidation.Guess(Guess, nameof(Guess));
    }
}

public sealed record SurrenderRequest : ProtocolMessage
{
    [JsonConstructor]
    public SurrenderRequest(Guid messageId, Guid sessionId)
        : base(messageId)
    {
        SessionId = ProtocolValidation.RequiredId(sessionId, nameof(sessionId));
    }

    public Guid SessionId { get; }

    internal override void Validate()
    {
        base.Validate();
        _ = ProtocolValidation.RequiredId(SessionId, nameof(SessionId));
    }
}

/// <summary>
/// Result of a guess while a session may still be active. It intentionally contains no secret-number field.
/// </summary>
public sealed record GuessResponse : ProtocolMessage
{
    [JsonConstructor]
    public GuessResponse(
        Guid messageId,
        Guid sessionId,
        string guess,
        int bulls,
        int cows,
        int attemptNumber,
        bool isComplete,
        bool isWon)
        : base(messageId)
    {
        SessionId = ProtocolValidation.RequiredId(sessionId, nameof(sessionId));
        Guess = ProtocolValidation.Guess(guess, nameof(guess));
        Bulls = ProtocolValidation.InRange(bulls, 0, Guess.Length, nameof(bulls));
        Cows = ProtocolValidation.InRange(cows, 0, Guess.Length, nameof(cows));
        AttemptNumber = ProtocolValidation.InRange(attemptNumber, 1, 1_000, nameof(attemptNumber));

        if (bulls + cows > Guess.Length)
        {
            throw new ArgumentException("The total number of bulls and cows cannot exceed the guess length.");
        }

        if (isWon && !isComplete)
        {
            throw new ArgumentException("A winning result must also be complete.", nameof(isComplete));
        }

        IsComplete = isComplete;
        IsWon = isWon;
    }

    public Guid SessionId { get; }

    public string Guess { get; }

    public int Bulls { get; }

    public int Cows { get; }

    public int AttemptNumber { get; }

    public bool IsComplete { get; }

    public bool IsWon { get; }

    internal override void Validate()
    {
        _ = new GuessResponse(MessageId, SessionId, Guess, Bulls, Cows, AttemptNumber, IsComplete, IsWon);
    }
}

/// <summary>
/// Final session response. The secret can only be represented after the session has ended.
/// </summary>
public sealed record GameEndedResponse : ProtocolMessage
{
    [JsonConstructor]
    public GameEndedResponse(
        Guid messageId,
        Guid sessionId,
        GameEndReason reason,
        string revealedSecret,
        int attemptsUsed)
        : base(messageId)
    {
        SessionId = ProtocolValidation.RequiredId(sessionId, nameof(sessionId));
        Reason = ProtocolValidation.DefinedEnum(reason, nameof(reason));
        RevealedSecret = ProtocolValidation.Guess(revealedSecret, nameof(revealedSecret));
        AttemptsUsed = ProtocolValidation.InRange(attemptsUsed, 0, 1_000, nameof(attemptsUsed));
    }

    public Guid SessionId { get; }

    public GameEndReason Reason { get; }

    public string RevealedSecret { get; }

    public int AttemptsUsed { get; }

    internal override void Validate()
    {
        _ = new GameEndedResponse(MessageId, SessionId, Reason, RevealedSecret, AttemptsUsed);
    }
}

public sealed record ErrorResponse : ProtocolMessage
{
    [JsonConstructor]
    public ErrorResponse(Guid messageId, Guid? sessionId, string code, string description)
        : base(messageId)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("A session ID must not be empty when supplied.", nameof(sessionId));
        }

        SessionId = sessionId;
        Code = ProtocolValidation.RequiredText(code, 64, nameof(code));
        Description = ProtocolValidation.RequiredText(description, 512, nameof(description));
    }

    public Guid? SessionId { get; }

    public string Code { get; }

    public string Description { get; }

    internal override void Validate()
    {
        _ = new ErrorResponse(MessageId, SessionId, Code, Description);
    }
}

public enum GameEndReason
{
    Won,
    AttemptsExhausted,
    Surrendered,
    TimedOut,
}

internal static class ProtocolValidation
{
    public static Guid RequiredId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("The identifier must not be empty.", parameterName);
        }

        return value;
    }

    public static int InRange(int value, int minimum, int maximum, string parameterName)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"The value must be between {minimum} and {maximum}.");
        }

        return value;
    }

    public static string Guess(string value, string parameterName)
    {
        string guess = RequiredText(value, 10, parameterName);
        if (guess.Any(character => character is < '0' or > '9'))
        {
            throw new ArgumentException("A guess must contain ASCII digits only.", parameterName);
        }

        return guess;
    }

    public static string RequiredText(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        if (value.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"The value cannot exceed {maximumLength} characters.");
        }

        return value;
    }

    public static TEnum DefinedEnum<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The value is not defined.");
        }

        return value;
    }
}
