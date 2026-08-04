using System.Security.Cryptography;
using CowBull.Application.Ports;
using CowBull.Domain.Games;

namespace CowBull.Infrastructure.Games;

public sealed class CryptographicSecretNumberGenerator : ISecretNumberGenerator
{
    private const string Digits = "0123456789";

    public string Generate(GameConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration.AllowDuplicateDigits
            ? GenerateWithDuplicates(configuration.NumberLength)
            : GenerateWithoutDuplicates(configuration.NumberLength);
    }

    private static string GenerateWithDuplicates(int length)
    {
        var result = new char[length];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        }

        return new string(result);
    }

    private static string GenerateWithoutDuplicates(int length)
    {
        char[] availableDigits = Digits.ToCharArray();

        // A partial Fisher-Yates shuffle chooses uniformly without replacement.
        for (var index = 0; index < length; index++)
        {
            var selectedIndex = RandomNumberGenerator.GetInt32(index, availableDigits.Length);
            (availableDigits[index], availableDigits[selectedIndex]) =
                (availableDigits[selectedIndex], availableDigits[index]);
        }

        return new string(availableDigits, 0, length);
    }
}
