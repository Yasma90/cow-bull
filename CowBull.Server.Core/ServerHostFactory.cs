using CowBull.Application.Games;
using CowBull.Infrastructure.Games;
using CowBull.Infrastructure.Identity;
using CowBull.Infrastructure.Networking;
using CowBull.Infrastructure.Persistence;

namespace CowBull.Server.Core;

public static class ServerHostFactory
{
    public static AuthoritativeGameServer CreateLoopbackHost(TimeSpan? readTimeout = null)
    {
        var gameService = new GameService(
            new InMemoryGameRepository(),
            new CryptographicSecretNumberGenerator(),
            new GuidGameIdGenerator(),
            TimeProvider.System);
        var configuration = new NetworkConfiguration(
            readTimeout: readTimeout ?? TimeSpan.FromMinutes(5));
        return new AuthoritativeGameServer(
            new AsyncTcpServer(configuration),
            new GameRequestHandler(gameService));
    }
}
