using System.Windows;
using CowBull.Application.Games;
using CowBull.Infrastructure.Games;
using CowBull.Infrastructure.Identity;
using CowBull.Infrastructure.Networking;
using CowBull.Infrastructure.Persistence;
using CowBullServer.Modern.Presentation;
using CowBullServer.Modern.Services;
using CowBullServer.Modern.ViewModels;

namespace CowBullServer.Modern;

public partial class App : Application, IDisposable
{
    private ServerViewModel? _viewModel;
    private int _disposeStarted;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var gameService = new GameService(
            new InMemoryGameRepository(),
            new CryptographicSecretNumberGenerator(),
            new GuidGameIdGenerator(),
            TimeProvider.System);
        // Loopback is the secure default while the protocol has no remote
        // authentication or transport encryption.
        var configuration = new NetworkConfiguration(
            readTimeout: TimeSpan.FromMinutes(5));
        var transport = new AsyncTcpServer(configuration);
        var handler = new GameRequestHandler(gameService);
        var server = new AuthoritativeGameServer(transport, handler);
        _viewModel = new ServerViewModel(server, new WpfUiDispatcher(Dispatcher));

        new MainWindow(_viewModel).Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Dispose();
        base.OnExit(e);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            return;
        }

        _viewModel?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _viewModel = null;
        GC.SuppressFinalize(this);
    }
}
