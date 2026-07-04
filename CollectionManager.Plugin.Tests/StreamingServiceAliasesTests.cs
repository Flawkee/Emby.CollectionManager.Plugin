using CollectionManager.Plugin.Helpers;
using Xunit;

namespace CollectionManager.Plugin.Tests;

public sealed class StreamingServiceAliasesTests
{
    [Theory]
    [InlineData("HBO Max")]
    [InlineData("Max Original")]
    [InlineData("Home Box Office")]
    [InlineData("Warner Bros. Pictures")]
    [InlineData("DC Entertainment")]
    public void GetStreamingServices_MapsMaxAndWarnerCatalogAliasesToHboMax(string studio)
    {
        var services = StreamingServiceAliases.GetStreamingServices(new[] { studio });

        Assert.Contains("HBO Max", services);
    }

    [Fact]
    public void GetStreamingServices_DeduplicatesMultipleHboMaxAliases()
    {
        var services = StreamingServiceAliases.GetStreamingServices(new[]
        {
            "HBO Max",
            "Warner Bros. Pictures",
            "DC Entertainment"
        });

        Assert.Equal(new[] { "HBO Max" }, services);
    }
}
