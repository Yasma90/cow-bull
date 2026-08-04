using CowBull.Application.Games;
using CowBull.Domain.Games;
using CowBull.Infrastructure.Games;
using CowBull.Infrastructure.Identity;
using CowBull.Infrastructure.Persistence;

var gameService = new GameService(
    new InMemoryGameRepository(),
    new CryptographicSecretNumberGenerator(),
    new GuidGameIdGenerator(),
    TimeProvider.System);
var configuration = new GameConfiguration(
    numberLength: 4,
    maxAttempts: 10,
    allowDuplicateDigits: false,
    timeout: TimeSpan.FromMinutes(5));

GameSnapshot game = gameService.StartGame(configuration);

Console.WriteLine("CowBull clean architecture demo");
Console.WriteLine("Guess a four-digit number with no duplicate digits.");
Console.WriteLine("Type 'quit' to surrender.");

while (game.Status == GameStatus.Active)
{
    Console.Write($"Guess ({game.RemainingAttempts} attempts remaining): ");
    string? input = Console.ReadLine();

    if (input is null ||
        string.Equals(input.Trim(), "quit", StringComparison.OrdinalIgnoreCase))
    {
        game = gameService.EndGame(game.GameId);
        break;
    }

    try
    {
        GuessResult result = gameService.SubmitGuess(game.GameId, input.Trim());
        game = result.Game;
        Console.WriteLine(
            $"{result.Attempt.Score.Bulls} bulls, {result.Attempt.Score.Cows} cows.");
    }
    catch (ArgumentException exception)
    {
        Console.WriteLine($"Invalid guess: {exception.Message}");
    }
    catch (GameInactiveException exception)
    {
        game = exception.Game;
    }
}

Console.WriteLine(
    $"Game ended: {game.Status}. The secret was {game.SecretNumber}.");
