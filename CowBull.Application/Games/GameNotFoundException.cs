namespace CowBull.Application.Games;

public sealed class GameNotFoundException : Exception
{
    public GameNotFoundException(Guid gameId)
        : base($"Game '{gameId}' was not found.")
    {
        GameId = gameId;
    }

    public Guid GameId { get; }
}
