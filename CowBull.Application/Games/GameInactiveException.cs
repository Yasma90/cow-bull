using CowBull.Domain.Games;

namespace CowBull.Application.Games;

public sealed class GameInactiveException : InvalidOperationException
{
    public GameInactiveException(GameSnapshot game, Exception innerException)
        : base(CreateMessage(game), innerException)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!game.IsTerminal)
        {
            throw new ArgumentException("The inactive game snapshot must be terminal.", nameof(game));
        }

        Game = game;
    }

    public GameSnapshot Game { get; }

    private static string CreateMessage(GameSnapshot game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return $"Game '{game.GameId}' is no longer active.";
    }
}
