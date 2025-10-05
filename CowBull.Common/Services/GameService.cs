using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CowBull.Common.Models;

namespace CowBull.Common.Services
{
    /// <summary>
    /// Interface for game logic operations
    /// </summary>
    public interface IGameService
    {
        /// <summary>
        /// Creates a new game session
        /// </summary>
        /// <param name="gameConfig">Game configuration</param>
        /// <returns>Game session</returns>
        Task<GameSession> CreateGameAsync(GameConfiguration gameConfig);

        /// <summary>
        /// Processes a player's guess
        /// </summary>
        /// <param name="sessionId">Game session ID</param>
        /// <param name="guess">Player's guess</param>
        /// <returns>Game result</returns>
        Task<GameResult> ProcessGuessAsync(Guid sessionId, string guess);

        /// <summary>
        /// Gets the current game session
        /// </summary>
        /// <param name="sessionId">Game session ID</param>
        /// <returns>Game session or null if not found</returns>
        Task<GameSession> GetGameSessionAsync(Guid sessionId);

        /// <summary>
        /// Ends a game session
        /// </summary>
        /// <param name="sessionId">Game session ID</param>
        /// <returns>Final game session</returns>
        Task<GameSession> EndGameAsync(Guid sessionId);
    }

    /// <summary>
    /// Modern game service implementation using best practices
    /// </summary>
    public class GameService : IGameService
    {
        private readonly ILogger<GameService> _logger;
        private readonly Dictionary<Guid, GameSession> _activeSessions;
        private readonly Random _random;
        private readonly object _lockObject = new object();

        public GameService(ILogger<GameService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _activeSessions = new Dictionary<Guid, GameSession>();
            _random = new Random();
        }

        public Task<GameSession> CreateGameAsync(GameConfiguration gameConfig)
        {
            if (gameConfig == null)
                throw new ArgumentNullException(nameof(gameConfig));

            if (!gameConfig.IsValid())
                throw new ArgumentException("Invalid game configuration", nameof(gameConfig));

            lock (_lockObject)
            {
                var sessionId = Guid.NewGuid();
                var secretNumber = GenerateSecretNumber(gameConfig);
                
                var session = new GameSession
                {
                    SessionId = sessionId,
                    SecretNumber = secretNumber,
                    Configuration = gameConfig,
                    StartTime = DateTime.UtcNow,
                    Status = GameStatus.Active,
                    Attempts = new List<GameAttempt>(),
                    MaxAttempts = gameConfig.MaxAttempts
                };

                _activeSessions[sessionId] = session;
                
                _logger.LogInformation("Created new game session {SessionId} with secret number {SecretNumber}", 
                    sessionId, secretNumber);

                return Task.FromResult(session);
            }
        }

        public Task<GameResult> ProcessGuessAsync(Guid sessionId, string guess)
        {
            if (string.IsNullOrWhiteSpace(guess))
                throw new ArgumentException("Guess cannot be null or empty", nameof(guess));

            lock (_lockObject)
            {
                if (!_activeSessions.TryGetValue(sessionId, out var session))
                {
                    _logger.LogWarning("Game session {SessionId} not found", sessionId);
                    return Task.FromResult(new GameResult
                    {
                        IsValid = false,
                        Message = "Game session not found"
                    });
                }

                if (session.Status != GameStatus.Active)
                {
                    _logger.LogWarning("Game session {SessionId} is not active (status: {Status})", 
                        sessionId, session.Status);
                    return Task.FromResult(new GameResult
                    {
                        IsValid = false,
                        Message = "Game is not active"
                    });
                }

                // Validate guess format
                var validationResult = ValidateGuess(guess, session.Configuration);
                if (!validationResult.IsValid)
                {
                    return Task.FromResult(validationResult);
                }

                // Calculate bulls and cows
                var (bulls, cows) = CalculateBullsAndCows(session.SecretNumber, guess);
                
                var attempt = new GameAttempt
                {
                    AttemptNumber = session.Attempts.Count + 1,
                    Guess = guess,
                    Bulls = bulls,
                    Cows = cows,
                    Timestamp = DateTime.UtcNow
                };

                session.Attempts.Add(attempt);

                var result = new GameResult
                {
                    IsValid = true,
                    Bulls = bulls,
                    Cows = cows,
                    AttemptNumber = attempt.AttemptNumber,
                    IsGameWon = bulls == session.Configuration.NumberLength,
                    IsGameOver = bulls == session.Configuration.NumberLength || 
                                session.Attempts.Count >= session.MaxAttempts
                };

                if (result.IsGameWon)
                {
                    session.Status = GameStatus.Won;
                    session.EndTime = DateTime.UtcNow;
                    result.Message = "Congratulations! You guessed the number!";
                    _logger.LogInformation("Game session {SessionId} won in {Attempts} attempts", 
                        sessionId, session.Attempts.Count);
                }
                else if (session.Attempts.Count >= session.MaxAttempts)
                {
                    session.Status = GameStatus.Lost;
                    session.EndTime = DateTime.UtcNow;
                    result.Message = $"Game over! The number was {session.SecretNumber}";
                    _logger.LogInformation("Game session {SessionId} lost after {Attempts} attempts", 
                        sessionId, session.Attempts.Count);
                }
                else
                {
                    var remainingAttempts = session.MaxAttempts - session.Attempts.Count;
                    result.Message = $"Try again! {remainingAttempts} attempts remaining";
                }

                result.RemainingAttempts = Math.Max(0, session.MaxAttempts - session.Attempts.Count);
                result.SecretNumber = result.IsGameOver ? session.SecretNumber : null;

                _logger.LogDebug("Processed guess {Guess} for session {SessionId}: {Bulls} bulls, {Cows} cows", 
                    guess, sessionId, bulls, cows);

                return Task.FromResult(result);
            }
        }

        public Task<GameSession> GetGameSessionAsync(Guid sessionId)
        {
            lock (_lockObject)
            {
                _activeSessions.TryGetValue(sessionId, out var session);
                return Task.FromResult(session);
            }
        }

        public Task<GameSession> EndGameAsync(Guid sessionId)
        {
            lock (_lockObject)
            {
                if (_activeSessions.TryGetValue(sessionId, out var session))
                {
                    if (session.Status == GameStatus.Active)
                    {
                        session.Status = GameStatus.Abandoned;
                        session.EndTime = DateTime.UtcNow;
                    }

                    _activeSessions.Remove(sessionId);
                    _logger.LogInformation("Ended game session {SessionId}", sessionId);
                    return Task.FromResult(session);
                }

                return Task.FromResult<GameSession>(null);
            }
        }

        private string GenerateSecretNumber(GameConfiguration config)
        {
            var digits = new List<int>();
            
            if (config.AllowDuplicateDigits)
            {
                // Generate random digits allowing duplicates
                for (int i = 0; i < config.NumberLength; i++)
                {
                    digits.Add(_random.Next(0, 10));
                }
            }
            else
            {
                // Generate unique digits
                var availableDigits = Enumerable.Range(0, 10).ToList();
                
                for (int i = 0; i < config.NumberLength && availableDigits.Count > 0; i++)
                {
                    var index = _random.Next(availableDigits.Count);
                    digits.Add(availableDigits[index]);
                    availableDigits.RemoveAt(index);
                }
            }

            return string.Join("", digits);
        }

        private GameResult ValidateGuess(string guess, GameConfiguration config)
        {
            if (guess.Length != config.NumberLength)
            {
                return new GameResult
                {
                    IsValid = false,
                    Message = $"Guess must be exactly {config.NumberLength} digits long"
                };
            }

            if (!guess.All(char.IsDigit))
            {
                return new GameResult
                {
                    IsValid = false,
                    Message = "Guess must contain only digits"
                };
            }

            if (!config.AllowDuplicateDigits && guess.Distinct().Count() != guess.Length)
            {
                return new GameResult
                {
                    IsValid = false,
                    Message = "Guess cannot contain duplicate digits"
                };
            }

            return new GameResult { IsValid = true };
        }

        private (int bulls, int cows) CalculateBullsAndCows(string secret, string guess)
        {
            var bulls = 0;
            var cows = 0;
            var secretCounts = new Dictionary<char, int>();
            var guessCounts = new Dictionary<char, int>();

            // Count bulls and prepare for cow calculation
            for (int i = 0; i < secret.Length; i++)
            {
                if (secret[i] == guess[i])
                {
                    bulls++;
                }
                else
                {
                    // Count non-bull digits for cow calculation
                    secretCounts[secret[i]] = secretCounts.GetValueOrDefault(secret[i], 0) + 1;
                    guessCounts[guess[i]] = guessCounts.GetValueOrDefault(guess[i], 0) + 1;
                }
            }

            // Calculate cows
            foreach (var digit in guessCounts.Keys)
            {
                if (secretCounts.ContainsKey(digit))
                {
                    cows += Math.Min(secretCounts[digit], guessCounts[digit]);
                }
            }

            return (bulls, cows);
        }
    }

    /// <summary>
    /// Represents a game session
    /// </summary>
    public class GameSession
    {
        public Guid SessionId { get; set; }
        public string SecretNumber { get; set; }
        public GameConfiguration Configuration { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public GameStatus Status { get; set; }
        public List<GameAttempt> Attempts { get; set; } = new List<GameAttempt>();
        public int MaxAttempts { get; set; }
        public TimeSpan? Duration => EndTime?.Subtract(StartTime);
    }

    /// <summary>
    /// Represents a game attempt
    /// </summary>
    public class GameAttempt
    {
        public int AttemptNumber { get; set; }
        public string Guess { get; set; }
        public int Bulls { get; set; }
        public int Cows { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Represents the result of processing a guess
    /// </summary>
    public class GameResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public int Bulls { get; set; }
        public int Cows { get; set; }
        public int AttemptNumber { get; set; }
        public bool IsGameWon { get; set; }
        public bool IsGameOver { get; set; }
        public int RemainingAttempts { get; set; }
        public string SecretNumber { get; set; }
    }

    /// <summary>
    /// Game status enumeration
    /// </summary>
    public enum GameStatus
    {
        Active,
        Won,
        Lost,
        Abandoned
    }
}