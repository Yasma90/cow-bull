using CowBull.Server.Core;
using CowBullServer.Modern.Presentation;
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

    [Fact]
    public async Task Accept_loop_fault_marks_the_server_stopped()
    {
        var server = new FakeGameServerHost();
        await using var viewModel = new ServerViewModel(server, new ImmediateDispatcher());
        await viewModel.StartCommand.ExecuteAsync();

        server.RaiseFault("The accept loop stopped unexpectedly.");

        Assert.False(viewModel.IsRunning);
        Assert.Equal("The accept loop stopped unexpectedly.", viewModel.ServerStatus);
        Assert.Contains(
            viewModel.Activity,
            entry => entry.Contains("accept loop", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DisposeAsync_completes_when_the_caller_sync_context_is_blocked()
    {
        var server = new FakeGameServerHost { YieldOnDispose = true };
        var viewModel = new ServerViewModel(server, new ImmediateDispatcher());
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new BlockingSynchronizationContext());
        Task disposeTask;
        try
        {
            disposeTask = viewModel.DisposeAsync().AsTask();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        await disposeTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private sealed class BlockingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state)
        {
        }

        public override void Send(SendOrPostCallback callback, object? state) =>
            throw new InvalidOperationException("A blocked caller context cannot run the continuation.");
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

        public event EventHandler<ServerFaultEventArgs>? Faulted;

        public bool YieldOnDispose { get; init; }

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

        public async ValueTask DisposeAsync()
        {
            IsRunning = false;
            if (YieldOnDispose)
            {
                await Task.Delay(30).ConfigureAwait(false);
            }
        }

        public void RaiseFault(string message) =>
            Faulted?.Invoke(this, new ServerFaultEventArgs(message));

        public void RaiseActivity(string message) =>
            ActivityOccurred?.Invoke(
                this,
                new ServerActivityEventArgs(message, ConnectedClientCount));
    }
}
