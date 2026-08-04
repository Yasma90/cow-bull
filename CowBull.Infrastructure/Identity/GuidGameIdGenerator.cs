using CowBull.Application.Ports;

namespace CowBull.Infrastructure.Identity;

public sealed class GuidGameIdGenerator : IGameIdGenerator
{
    public Guid Create() => Guid.NewGuid();
}
