using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CowBull.Common.Infrastructure;
using CowBull.Common.Services;
using CowBull.Common.Models;

namespace CowBull.Demo;

/// <summary>
/// Demonstration of the modern CowBull architecture
/// This console application shows how to use the new communication layer
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== CowBull Modern Architecture Demo ===");
        Console.WriteLine("This demo shows the new client-server communication.");
        Console.WriteLine();

        // Create host builder with dependency injection
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Register configuration
                services.AddSingleton<NetworkConfiguration>(provider =>
                    new NetworkConfiguration
                    {
                        ServerAddress = "127.0.0.1",
                        Port = 4510,
                        ConnectionTimeoutMs = 30000,
                        ReceiveTimeoutMs = 30000,
                        SendTimeoutMs = 10000,
                        BufferSize = 8192,
                        MaxMessageSize = 1024 * 1024,
                        KeepAlive = true,
                        HeartbeatIntervalMs = 30000,
                        RetryAttempts = 3,
                        RetryDelayMs = 1000
                    });

                // Register game configuration
                services.AddSingleton<GameConfiguration>(provider =>
                    new GameConfiguration
                    {
                        NumberLength = 4,
                        MaxAttempts = 10,
                        AllowDuplicateDigits = false,
                        GameTimeoutMinutes = 30
                    });

                // Register services
                services.AddSingleton<IGameService, GameService>();
                services.AddTransient<AsyncTcpServer>();
                services.AddTransient<AsyncTcpClient>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        try
        {
            await RunDemoAsync(host.Services);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Demo failed: {ex.Message}");
        }
        finally
        {
            await host.StopAsync();
        }
    }

    static async Task RunDemoAsync(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        var networkConfig = services.GetRequiredService<NetworkConfiguration>();
        var gameService = services.GetRequiredService<IGameService>();

        logger.LogInformation("Starting CowBull Demo");

        // Ask user what they want to demo
        Console.WriteLine("Choose demo mode:");
        Console.WriteLine("1. Start Server");
        Console.WriteLine("2. Start Client");
        Console.WriteLine("3. Game Service Test");
        Console.WriteLine("4. Full Integration Test");
        Console.Write("Enter choice (1-4): ");

        var choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                await RunServerDemo(services, logger, networkConfig, gameService);
                break;
            case "2":
                await RunClientDemo(services, logger, networkConfig);
                break;
            case "3":
                await RunGameServiceTest(gameService, logger);
                break;
            case "4":
                await RunIntegrationTest(services, logger, networkConfig, gameService);
                break;
            default:
                Console.WriteLine("Invalid choice. Running integration test by default.");
                await RunIntegrationTest(services, logger, networkConfig, gameService);
                break;
        }
    }

    static async Task RunServerDemo(IServiceProvider services, ILogger logger, NetworkConfiguration config, IGameService gameService)
    {
        logger.LogInformation("Starting Server Demo");

        using var server = new AsyncTcpServer(config, services.GetRequiredService<ILogger<AsyncTcpServer>>());
        
        // Setup event handlers
        server.ClientConnected += (sender, e) =>
            logger.LogInformation("Client connected: {ClientId} from {Endpoint}", e.ClientId, e.Endpoint);
        
        server.ClientDisconnected += (sender, e) =>
            logger.LogInformation("Client disconnected: {ClientId} - {Reason}", e.ClientId, e.Reason);
        
        server.MessageReceived += async (sender, e) =>
        {
            logger.LogInformation("Message from client {ClientId}: {Message}", e.ClientId, e.Message);
            
            // Echo the message back
            await server.SendMessageToClientAsync(e.ClientId, $"Echo: {e.Message}");
        };

        // Start server
        var started = await server.StartAsync();
        if (!started)
        {
            logger.LogError("Failed to start server");
            return;
        }

        logger.LogInformation("Server started on {Address}:{Port}", config.ServerAddress, config.Port);
        logger.LogInformation("Press any key to stop server...");
        
        Console.ReadKey();
        
        await server.StopAsync();
        logger.LogInformation("Server stopped");
    }

    static async Task RunClientDemo(IServiceProvider services, ILogger logger, NetworkConfiguration config)
    {
        logger.LogInformation("Starting Client Demo");

        using var client = new AsyncTcpClient(config, services.GetRequiredService<ILogger<AsyncTcpClient>>());
        
        // Setup event handlers
        client.ConnectionStatusChanged += (sender, e) =>
            logger.LogInformation("Connection status: {IsConnected} - {Reason}", e.IsConnected, e.Reason);
        
        client.MessageReceived += (sender, e) =>
            logger.LogInformation("Message from server: {Message}", e.Message);

        // Connect to server
        Console.WriteLine("Make sure the server is running first!");
        Console.WriteLine("Press any key to connect...");
        Console.ReadKey();

        var connected = await client.ConnectAsync();
        if (!connected)
        {
            logger.LogError("Failed to connect to server");
            return;
        }

        logger.LogInformation("Connected to server");

        // Send some test messages
        // Send string messages
        var stringMessages = new[] { "Hello Server!", "This is a test message" };
        foreach (var message in stringMessages)
        {
            await client.SendMessageAsync(message);
            await Task.Delay(1000);
        }

        // Send game message
        var gameMessage = new GameMessage
        {
            ActionType = GameActionType.NewGame,
            Context = "Starting new game"
        };
        await client.SendMessageAsync(gameMessage);
        await Task.Delay(1000);

        Console.WriteLine("Press any key to disconnect...");
        Console.ReadKey();
        
        await client.DisconnectAsync();
        logger.LogInformation("Client disconnected");
    }

    static async Task RunGameServiceTest(IGameService gameService, ILogger logger)
    {
        logger.LogInformation("Starting Game Service Test");

        var gameConfig = new GameConfiguration
        {
            NumberLength = 4,
            MaxAttempts = 10,
            AllowDuplicateDigits = false
        };

        // Create a new game
        var session = await gameService.CreateGameAsync(gameConfig);
        logger.LogInformation("Created game session {SessionId} with secret number {SecretNumber}", 
            session.SessionId, session.SecretNumber);

        Console.WriteLine($"Game created! Secret number: {session.SecretNumber}");
        Console.WriteLine("Try to guess the 4-digit number (no duplicates):");

        while (session.Status == GameStatus.Active)
        {
            Console.Write("Enter your guess: ");
            var guess = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(guess))
                continue;

            var result = await gameService.ProcessGuessAsync(session.SessionId, guess);
            
            if (!result.IsValid)
            {
                Console.WriteLine($"Invalid guess: {result.Message}");
                continue;
            }

            Console.WriteLine($"Result: {result.Bulls} Bulls, {result.Cows} Cows");
            Console.WriteLine($"Message: {result.Message}");
            Console.WriteLine($"Attempts remaining: {result.RemainingAttempts}");
            Console.WriteLine();

            if (result.IsGameOver)
            {
                if (result.IsGameWon)
                {
                    Console.WriteLine("🎉 Congratulations! You won!");
                }
                else
                {
                    Console.WriteLine($"😞 Game over! The number was: {result.SecretNumber}");
                }
                break;
            }

            // Update session
            session = await gameService.GetGameSessionAsync(session.SessionId);
        }

        // End the game
        await gameService.EndGameAsync(session.SessionId);
        logger.LogInformation("Game service test completed");
    }

    static async Task RunIntegrationTest(IServiceProvider services, ILogger logger, NetworkConfiguration config, IGameService gameService)
    {
        logger.LogInformation("Starting Integration Test");

        // Start server
        using var server = new AsyncTcpServer(config, services.GetRequiredService<ILogger<AsyncTcpServer>>());
        var gameSessionId = Guid.Empty;

        server.ClientConnected += async (sender, e) =>
        {
            logger.LogInformation("Client connected: {ClientId}", e.ClientId);
            
            // Start a new game for this client
            var session = await gameService.CreateGameAsync(new GameConfiguration
            {
                NumberLength = 4,
                MaxAttempts = 10,
                AllowDuplicateDigits = false
            });
            
            gameSessionId = session.SessionId;
            
            var welcomeMessage = new GameMessage
            {
                ActionType = GameActionType.NewGame,
                Number = session.SecretNumber,
                Context = "Welcome! Game started. Try to guess the 4-digit number!"
            };
            
            await server.SendMessageToClientAsync(e.ClientId, welcomeMessage);
        };

        server.MessageReceived += async (sender, e) =>
        {
            logger.LogInformation("Processing guess from client {ClientId}: {Message}", e.ClientId, e.Message);
            
            if (string.IsNullOrWhiteSpace(e.Message) || !e.Message.All(char.IsDigit))
                return;

            var result = await gameService.ProcessGuessAsync(gameSessionId, e.Message);
            
            var response = new GameMessage
            {
                ActionType = GameActionType.Response,
                Number = e.Message,
                Bulls = result.Bulls,
                Cows = result.Cows,
                Context = result.Message
            };

            await server.SendMessageToClientAsync(e.ClientId, response);

            if (result.IsGameOver)
            {
                var gameOverMessage = new GameMessage
                {
                    ActionType = GameActionType.GameOver,
                    Number = result.SecretNumber,
                    Context = result.IsGameWon ? "Congratulations! You won!" : "Game Over!"
                };
                
                await server.SendMessageToClientAsync(e.ClientId, gameOverMessage);
            }
        };

        var serverStarted = await server.StartAsync();
        if (!serverStarted)
        {
            logger.LogError("Failed to start server");
            return;
        }

        logger.LogInformation("Server started. Starting client...");

        // Wait a moment for server to be ready
        await Task.Delay(1000);

        // Start client
        using var client = new AsyncTcpClient(config, services.GetRequiredService<ILogger<AsyncTcpClient>>());
        
        client.MessageReceived += (sender, e) =>
            logger.LogInformation("Client received: {Message}", e.Message);

        var clientConnected = await client.ConnectAsync();
        if (!clientConnected)
        {
            logger.LogError("Failed to connect client");
            await server.StopAsync();
            return;
        }

        logger.LogInformation("Client connected. Sending test guesses...");

        // Send some test guesses
        var testGuesses = new[] { "1234", "5678", "9012", "3456" };
        
        foreach (var guess in testGuesses)
        {
            logger.LogInformation("Sending guess: {Guess}", guess);
            await client.SendMessageAsync(guess);
            await Task.Delay(2000); // Wait for response
        }

        Console.WriteLine("Integration test completed. Press any key to cleanup...");
        Console.ReadKey();

        await client.DisconnectAsync();
        await server.StopAsync();
        
        logger.LogInformation("Integration test completed");
    }
}