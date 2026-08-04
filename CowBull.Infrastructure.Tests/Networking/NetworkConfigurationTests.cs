using CowBull.Infrastructure.Networking;

namespace CowBull.Infrastructure.Tests.Networking;

public sealed class NetworkConfigurationTests
{
    [Fact]
    public void Constructor_TrimsAndStoresValidatedImmutableValues()
    {
        var configuration = new NetworkConfiguration(
            " 127.0.0.1 ",
            0,
            8_192,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(4));

        Assert.Equal("127.0.0.1", configuration.Host);
        Assert.Equal(0, configuration.Port);
        Assert.Equal(8_192, configuration.MaximumPayloadBytes);
        Assert.Equal(TimeSpan.FromSeconds(2), configuration.ConnectTimeout);
        Assert.Equal(TimeSpan.FromSeconds(3), configuration.ReadTimeout);
        Assert.Equal(TimeSpan.FromSeconds(4), configuration.WriteTimeout);
    }

    [Fact]
    public void Constructor_RejectsInvalidLimits()
    {
        Assert.Throws<ArgumentException>(() => new NetworkConfiguration(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NetworkConfiguration(port: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NetworkConfiguration(port: 65_536));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NetworkConfiguration(maximumPayloadBytes: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new NetworkConfiguration(connectTimeout: TimeSpan.Zero));
    }
}
