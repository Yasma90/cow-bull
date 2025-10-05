using System;
using Microsoft.Extensions.Logging;

namespace CowBull.Common.Services
{
    /// <summary>
    /// Configuration for network communication
    /// </summary>
    public class NetworkConfiguration
    {
        /// <summary>
        /// Server IP address
        /// </summary>
        public string ServerAddress { get; set; } = "127.0.0.1";

        /// <summary>
        /// Server port
        /// </summary>
        public int Port { get; set; } = 4510;

        /// <summary>
        /// Connection timeout in milliseconds
        /// </summary>
        public int ConnectionTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Receive timeout in milliseconds
        /// </summary>
        public int ReceiveTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Send timeout in milliseconds
        /// </summary>
        public int SendTimeoutMs { get; set; } = 10000;

        /// <summary>
        /// Buffer size for network operations
        /// </summary>
        public int BufferSize { get; set; } = 8192;

        /// <summary>
        /// Maximum message size in bytes
        /// </summary>
        public int MaxMessageSize { get; set; } = 1024 * 1024; // 1MB

        /// <summary>
        /// Enable keep-alive packets
        /// </summary>
        public bool KeepAlive { get; set; } = true;

        /// <summary>
        /// Heartbeat interval in milliseconds
        /// </summary>
        public int HeartbeatIntervalMs { get; set; } = 30000;

        /// <summary>
        /// Number of retry attempts for failed operations
        /// </summary>
        public int RetryAttempts { get; set; } = 3;

        /// <summary>
        /// Delay between retry attempts in milliseconds
        /// </summary>
        public int RetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Validates the configuration
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(ServerAddress) &&
                   Port > 0 && Port <= 65535 &&
                   ConnectionTimeoutMs > 0 &&
                   ReceiveTimeoutMs > 0 &&
                   SendTimeoutMs > 0 &&
                   BufferSize > 0 &&
                   MaxMessageSize > 0 &&
                   HeartbeatIntervalMs > 0 &&
                   RetryAttempts >= 0 &&
                   RetryDelayMs >= 0;
        }
    }

    /// <summary>
    /// Game configuration settings
    /// </summary>
    public class GameConfiguration
    {
        /// <summary>
        /// Number of digits in the number to guess
        /// </summary>
        public int NumberLength { get; set; } = 4;

        /// <summary>
        /// Maximum number of attempts allowed
        /// </summary>
        public int MaxAttempts { get; set; } = 10;

        /// <summary>
        /// Allow duplicate digits in the number
        /// </summary>
        public bool AllowDuplicateDigits { get; set; } = false;

        /// <summary>
        /// Game timeout in minutes
        /// </summary>
        public int GameTimeoutMinutes { get; set; } = 30;

        /// <summary>
        /// Validates the game configuration
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        public bool IsValid()
        {
            return NumberLength > 0 && NumberLength <= 10 &&
                   MaxAttempts > 0 &&
                   GameTimeoutMinutes > 0;
        }
    }

    /// <summary>
    /// Application configuration
    /// </summary>
    public class AppConfiguration
    {
        /// <summary>
        /// Network configuration
        /// </summary>
        public NetworkConfiguration Network { get; set; } = new NetworkConfiguration();

        /// <summary>
        /// Game configuration
        /// </summary>
        public GameConfiguration Game { get; set; } = new GameConfiguration();

        /// <summary>
        /// Logging level
        /// </summary>
        public LogLevel LogLevel { get; set; } = LogLevel.Information;

        /// <summary>
        /// Application name
        /// </summary>
        public string ApplicationName { get; set; } = "CowBull";

        /// <summary>
        /// Application version
        /// </summary>
        public string Version { get; set; } = "2.0.0";

        /// <summary>
        /// Validates the entire configuration
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        public bool IsValid()
        {
            return Network?.IsValid() == true &&
                   Game?.IsValid() == true &&
                   !string.IsNullOrWhiteSpace(ApplicationName) &&
                   !string.IsNullOrWhiteSpace(Version);
        }
    }
}