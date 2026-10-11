using System.Windows;
using CowBull.Server.Core;
using CowBullServer.Modern.Presentation;
using CowBullServer.Modern.ViewModels;

namespace CowBullServer.Modern;

public partial class App : Application, IDisposable
{
    private ServerViewModel? _viewModel;
    private int _disposeStarted;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _viewModel = new ServerViewModel(
            ServerHostFactory.CreateLoopbackHost(),
            new WpfUiDispatcher(Dispatcher));

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

        ServerViewModel? viewModel = _viewModel;
        _viewModel = null;
        if (viewModel is not null)
        {
            Task.Run(() => viewModel.DisposeAsync().AsTask()).GetAwaiter().GetResult();
        }

        GC.SuppressFinalize(this);
    }
}
