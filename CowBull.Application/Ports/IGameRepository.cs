using CowBull.Domain.Games;

namespace CowBull.Application.Ports;

public interface IGameRepository
{
    GameSession? GetById(Guid gameId);

    void Add(GameSession game);

    void Update(GameSession game);

    bool Remove(Guid gameId);
}
