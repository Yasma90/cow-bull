using CowBull.Domain.Games;

namespace CowBull.Application.Ports;

public interface ISecretNumberGenerator
{
    string Generate(GameConfiguration configuration);
}
