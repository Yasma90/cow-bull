using CowBull.Domain.Games;

namespace CowBull.Application.Abstractions;

public interface IGameService
{
    GameSnapshot StartGame(GameConfiguration configuration);

    GuessResult SubmitGuess(Guid gameId, string guess);

    GameSnapshot GetGame(Guid gameId);

    GameSnapshot EndGame(Guid gameId);
}
