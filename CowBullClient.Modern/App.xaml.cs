using System.Windows;
using CowBull.Infrastructure.Networking;
using CowBullClient.Modern.Presentation;
using CowBullClient.Modern.Services;
using CowBullClient.Modern.ViewModels;

namespace CowBullClient.Modern;

public partial class App : Application, IDisposable
{
    private ClientViewModel? _viewModel;
    private int _disposeStarted;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configuration = new NetworkConfiguration(readTimeout: TimeSpan.FromMinutes(5));
        var transport = new AsyncTcpClient(configuration);
        var gameClient = new TcpGameClient(transport);
        _viewModel = new ClientViewModel(gameClient, new WpfUiDispatcher(Dispatcher));

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
