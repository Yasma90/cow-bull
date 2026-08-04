using CowBullServer.Modern.Presentation;
using CowBullServer.Modern.Services;
using CowBullServer.Modern.ViewModels;

namespace CowBull.Presentation.Tests.Server;

public sealed class ServerViewModelTests
{
    [Fact]
    public async Task Activity_is_bounded_to_the_most_recent_two_hundred_entries()
    {
        var server = new FakeGameServerHost();
        await using var viewModel = new ServerViewModel(server, new ImmediateDispatcher());

        for (var index = 0; index < 205; index++)
        {
            server.RaiseActivity($"Event {index}");
        }

        Assert.Equal(200, viewModel.Activity.Count);
        Assert.Contains("Event 5", viewModel.Activity[0], StringComparison.Ordinal);
        Assert.Contains("Event 204", viewModel.Activity[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Activity_burst_schedules_one_bounded_dispatch_batch()
    {
        var server = new FakeGameServerHost();
        var dispatcher = new QueuedDispatcher();
        await using var viewModel = new ServerViewModel(server, dispatcher);

        for (var index = 0; index < 205; index++)
        {
            server.RaiseActivity($"Event {index}");
        }

        Assert.Equal(1, dispatcher.PendingCount);
        Assert.Empty(viewModel.Activity);

        dispatcher.RunAll();

        Assert.Equal(200, viewModel.Activity.Count);
        Assert.Contains("Event 5", viewModel.Activity[0], StringComparison.Ordinal);
        Assert.Contains("Event 204", viewModel.Activity[^1], StringComparison.Ordinal);
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Post(Action action) => action();
    }

    private sealed class QueuedDispatcher : IUiDispatcher
    {
        private readonly Queue<Action> _pending = new();

        public int PendingCount => _pending.Count;

        public void Post(Action action) => _pending.Enqueue(action);

        public void RunAll()
        {
            while (_pending.TryDequeue(out Action? action))
            {
                action();
            }
        }
    }

    private sealed class FakeGameServerHost : IGameServerHost
    {
        public event EventHandler<ServerActivityEventArgs>? ActivityOccurred;

        public bool IsRunning { get; private set; }

        public int ConnectedClientCount { get; private set; }

        public string Endpoint => "127.0.0.1:4510";

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            IsRunning = false;
            ConnectedClientCount = 0;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsRunning = false;
            return ValueTask.CompletedTask;
        }

        public void RaiseActivity(string message) =>
            ActivityOccurred?.Invoke(
                this,
                new ServerActivityEventArgs(message, ConnectedClientCount));
    }
}
