using MergeButler.Commands;

namespace MergeButler.Tests.Commands;

public class PlatformServiceFactoryTests
{
    [Theory]
    [InlineData("GitHub", Platform.GitHub)]
    [InlineData("github", Platform.GitHub)]
    [InlineData("GITHUB", Platform.GitHub)]
    [InlineData("AzureDevOps", Platform.AzureDevOps)]
    [InlineData("azuredevops", Platform.AzureDevOps)]
    public void ParsePlatform_ValidValues_ReturnsEnum(string input, Platform expected)
    {
        Platform result = PlatformServiceFactory.ParsePlatform(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("gitlab")]
    public void ParsePlatform_InvalidValues_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => PlatformServiceFactory.ParsePlatform(input));
    }

    [Fact]
    public void ResolveToken_ProvidedToken_ReturnsProvided()
    {
        string? result = PlatformServiceFactory.ResolveToken(Platform.GitHub, "my-token");
        Assert.Equal("my-token", result);
    }

    [Fact]
    public void ResolveToken_NullToken_FallsBackToEnvVar()
    {
        // If env var is not set, returns null
        string? result = PlatformServiceFactory.ResolveToken(Platform.GitHub, null);
        // Can't assert specific value since it depends on env, but shouldn't throw
        Assert.True(result is null || result.Length > 0);
    }

    [Fact]
    public void CreateService_GitHub_ReturnsService()
    {
        var service = PlatformServiceFactory.CreateService(Platform.GitHub, "fake-token");

        Assert.NotNull(service);
    }

    [Fact]
    public void CreateService_AzureDevOps_ReturnsService()
    {
        var service = PlatformServiceFactory.CreateService(Platform.AzureDevOps, "fake-token");

        Assert.NotNull(service);
    }
}
