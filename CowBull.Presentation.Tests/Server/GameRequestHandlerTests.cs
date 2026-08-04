using CowBull.Application.Games;
using CowBull.Application.Ports;
using CowBull.Domain.Games;
using CowBull.Infrastructure.Persistence;
using CowBull.Infrastructure.Protocol;
using CowBullServer.Modern.Services;

namespace CowBull.Presentation.Tests.Server;

public sealed class GameRequestHandlerTests
{
    private static readonly Guid ClientId =
        Guid.Parse("74be08ba-08b0-4b50-906e-9db35facf540");
    private static readonly Guid OtherClientId =
        Guid.Parse("cb3c2f98-1b57-4c77-b89a-ad0c3cc3f170");
    private static readonly Guid SessionId =
        Guid.Parse("d8b654d2-eb7b-4b60-bd54-fd9f03326f09");
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void New_game_request_creates_server_owned_session()
    {
        var context = CreateContext();
        var messageId = Guid.NewGuid();

        ProtocolMessage response = Assert.Single(
            context.Handler.Handle(
                ClientId,
                new NewGameRequest(messageId, numberLength: 4, maximumAttempts: 7)));

        var created = Assert.IsType<NewGameResponse>(response);
        Assert.Equal(messageId, created.MessageId);
        Assert.Equal(SessionId, created.SessionId);
        Assert.Equal(4, created.NumberLength);
        Assert.Equal(7, created.MaximumAttempts);
        Assert.Null(context.Service.GetGame(SessionId).SecretNumber);
    }

    [Fact]
    public void Client_cannot_guess_another_clients_session()
    {
        var context = CreateContext();
        StartGame(context);

        ProtocolMessage response = Assert.Single(
            context.Handler.Handle(
                OtherClientId,
                new GuessRequest(Guid.NewGuid(), SessionId, "0123")));

        var error = Assert.IsType<ErrorResponse>(response);
        Assert.Equal("sessionNotOwned", error.Code);
        Assert.Empty(context.Service.GetGame(SessionId).Attempts);
    }

    [Fact]
    public void Guess_responses_preserve_attempt_order_and_score()
    {
        var context = CreateContext();
        StartGame(context);

        var first = Assert.IsType<GuessResponse>(
            Assert.Single(
                context.Handler.Handle(
                    ClientId,
                    new GuessRequest(Guid.NewGuid(), SessionId, "1023"))));
        var second = Assert.IsType<GuessResponse>(
            Assert.Single(
                context.Handler.Handle(
                    ClientId,
                    new GuessRequest(Guid.NewGuid(), SessionId, "4567"))));

        Assert.Equal(1, first.AttemptNumber);
        Assert.Equal(2, first.Bulls);
        Assert.Equal(2, first.Cows);
        Assert.Equal(2, second.AttemptNumber);
    }

    [Fact]
    public void Winning_guess_returns_score_then_correlated_terminal_response()
    {
        var context = CreateContext();
        StartGame(context);
        var requestId = Guid.NewGuid();

        IReadOnlyList<ProtocolMessage> responses = context.Handler.Handle(
            ClientId,
            new GuessRequest(requestId, SessionId, "0123"));

        var score = Assert.IsType<GuessResponse>(responses[0]);
        var ended = Assert.IsType<GameEndedResponse>(responses[1]);
        Assert.True(score.IsComplete);
        Assert.True(score.IsWon);
        Assert.Equal(requestId, score.MessageId);
        Assert.Equal(requestId, ended.MessageId);
        Assert.Equal(GameEndReason.Won, ended.Reason);
        Assert.Equal("0123", ended.RevealedSecret);
    }

    [Fact]
    public void Guess_just_before_deadline_is_scored_and_at_deadline_times_out()
    {
        var context = CreateContext();
        StartGame(context);
        context.Clock.SetUtcNow(Now.AddMinutes(4).AddTicks(-1));
        var activeResponse = Assert.IsType<GuessResponse>(
            Assert.Single(
                context.Handler.Handle(
                    ClientId,
                    new GuessRequest(Guid.NewGuid(), SessionId, "4567"))));
        Assert.False(activeResponse.IsComplete);

        context.Clock.SetUtcNow(Now.AddMinutes(4));
        var requestId = Guid.NewGuid();

        ProtocolMessage response = Assert.Single(
            context.Handler.Handle(
                ClientId,
                new GuessRequest(requestId, SessionId, "0123")));

        var ended = Assert.IsType<GameEndedResponse>(response);
        Assert.Equal(requestId, ended.MessageId);
        Assert.Equal(GameEndReason.TimedOut, ended.Reason);
        Assert.Equal("0123", ended.RevealedSecret);
        Assert.Equal(1, ended.AttemptsUsed);
    }

    [Fact]
    public void Disconnect_removes_client_ownership()
    {
        var context = CreateContext();
        StartGame(context);
        GameSession aggregate = Assert.IsType<GameSession>(
            context.Repository.GetById(SessionId));

        context.Handler.Disconnect(ClientId);

        ProtocolMessage response = Assert.Single(
            context.Handler.Handle(
                ClientId,
                new GuessRequest(Guid.NewGuid(), SessionId, "0123")));
        Assert.Equal("sessionNotOwned", Assert.IsType<ErrorResponse>(response).Code);
        Assert.Equal(GameStatus.Abandoned, aggregate.GetSnapshot(Now).Status);
        Assert.Equal(0, context.Repository.Count);
    }

    [Fact]
    public void Replacing_games_retains_only_the_current_active_session()
    {
        var context = CreateContext();

        for (var index = 0; index < 100; index++)
        {
            StartGame(context);
            Assert.Equal(1, context.Repository.Count);
        }

        context.Handler.Disconnect(ClientId);

        Assert.Equal(0, context.Repository.Count);
    }

    private static void StartGame(TestContext context) =>
        context.Handler.Handle(
            ClientId,
            new NewGameRequest(Guid.NewGuid(), numberLength: 4, maximumAttempts: 7));

    private static TestContext CreateContext()
    {
        var clock = new ManualTimeProvider(Now);
        var repository = new InMemoryGameRepository();
        var service = new GameService(
            repository,
            new StubSecretNumberGenerator("0123"),
            new StubGameIdGenerator(SessionId),
            clock);

        return new TestContext(
            new GameRequestHandler(service),
            service,
            repository,
            clock);
    }

    private sealed record TestContext(
        GameRequestHandler Handler,
        GameService Service,
        InMemoryGameRepository Repository,
        ManualTimeProvider Clock);

    private sealed class StubSecretNumberGenerator(string secret) : ISecretNumberGenerator
    {
        public string Generate(GameConfiguration configuration) => secret;
    }

    private sealed class StubGameIdGenerator(Guid gameId) : IGameIdGenerator
    {
        public Guid Create() => gameId;
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void SetUtcNow(DateTimeOffset value) => _utcNow = value;
    }
}
