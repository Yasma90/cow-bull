using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CowBull.Common.Models
{
    /// <summary>
    /// Base class for all network messages
    /// </summary>
    public abstract class MessageBase
    {
        /// <summary>
        /// Unique identifier for the message
        /// </summary>
        [Required]
        public Guid MessageId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Timestamp when the message was created
        /// </summary>
        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Type of the message
        /// </summary>
        [Required]
        public abstract MessageType MessageType { get; }
    }

    /// <summary>
    /// Game-related message for number guesses and responses
    /// </summary>
    public class GameMessage : MessageBase
    {
        public override MessageType MessageType => MessageType.Game;

        /// <summary>
        /// The number being guessed or generated
        /// </summary>
        [Required]
        [StringLength(10, MinimumLength = 1)]
        public string Number { get; set; }

        /// <summary>
        /// Number of bulls (correct digits in correct positions)
        /// </summary>
        [Range(0, int.MaxValue)]
        public int Bulls { get; set; }

        /// <summary>
        /// Number of cows (correct digits in wrong positions)
        /// </summary>
        [Range(0, int.MaxValue)]
        public int Cows { get; set; }

        /// <summary>
        /// Type of game action
        /// </summary>
        [Required]
        public GameActionType ActionType { get; set; }

        /// <summary>
        /// Additional message or context
        /// </summary>
        public string Context { get; set; }
    }

    /// <summary>
    /// System-level message for connection, authentication, etc.
    /// </summary>
    public class SystemMessage : MessageBase
    {
        public override MessageType MessageType => MessageType.System;

        /// <summary>
        /// System action type
        /// </summary>
        [Required]
        public SystemActionType ActionType { get; set; }

        /// <summary>
        /// Message content
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Additional data as JSON
        /// </summary>
        public string Data { get; set; }
    }

    /// <summary>
    /// Error message for communication failures
    /// </summary>
    public class ErrorMessage : MessageBase
    {
        public override MessageType MessageType => MessageType.Error;

        /// <summary>
        /// Error code
        /// </summary>
        [Required]
        public string ErrorCode { get; set; }

        /// <summary>
        /// Human-readable error message
        /// </summary>
        [Required]
        public string ErrorDescription { get; set; }

        /// <summary>
        /// Additional error details
        /// </summary>
        public string Details { get; set; }
    }

    /// <summary>
    /// Types of messages in the system
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MessageType
    {
        System,
        Game,
        Error
    }

    /// <summary>
    /// Types of game actions
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum GameActionType
    {
        NewGame,
        Guess,
        Response,
        GameOver,
        GenerateNumber
    }

    /// <summary>
    /// Types of system actions
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SystemActionType
    {
        Connect,
        Disconnect,
        Heartbeat,
        Authentication,
        Configuration
    }
}