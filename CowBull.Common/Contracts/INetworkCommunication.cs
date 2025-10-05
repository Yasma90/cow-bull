using System;
using System.Threading;
using System.Threading.Tasks;

namespace CowBull.Common.Contracts
{
    /// <summary>
    /// Defines the contract for network communication between client and server
    /// </summary>
    public interface INetworkCommunication : IDisposable
    {
        /// <summary>
        /// Event raised when a message is received
        /// </summary>
        event EventHandler<MessageReceivedEventArgs> MessageReceived;

        /// <summary>
        /// Event raised when connection status changes
        /// </summary>
        event EventHandler<ConnectionStatusChangedEventArgs> ConnectionStatusChanged;

        /// <summary>
        /// Gets the current connection status
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Connects to the remote endpoint asynchronously
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the async operation</returns>
        Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnects from the remote endpoint asynchronously
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the async operation</returns>
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message asynchronously
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the async operation</returns>
        Task<bool> SendMessageAsync(string message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a structured message asynchronously
        /// </summary>
        /// <typeparam name="T">Type of the message</typeparam>
        /// <param name="message">The message to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the async operation</returns>
        Task<bool> SendMessageAsync<T>(T message, CancellationToken cancellationToken = default) where T : class;
    }

    /// <summary>
    /// Event arguments for message received events
    /// </summary>
    public class MessageReceivedEventArgs : EventArgs
    {
        public string Message { get; }
        public DateTime Timestamp { get; }

        public MessageReceivedEventArgs(string message)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for connection status changes
    /// </summary>
    public class ConnectionStatusChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; }
        public string Reason { get; }

        public ConnectionStatusChangedEventArgs(bool isConnected, string? reason = null)
        {
            IsConnected = isConnected;
            Reason = reason ?? string.Empty;
        }
    }
}