namespace CowBullClient.Modern.Services;

public interface IResponseTimeout
{
    Task WaitAsync(CancellationToken cancellationToken);
}

public sealed class ResponseTimeout : IResponseTimeout
{
    private readonly TimeSpan _timeout;

    public ResponseTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "The response timeout must be greater than zero.");
        }

        _timeout = timeout;
    }

    public Task WaitAsync(CancellationToken cancellationToken) =>
        Task.Delay(_timeout, cancellationToken);
}
