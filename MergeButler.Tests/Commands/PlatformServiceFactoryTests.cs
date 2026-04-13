using MergeButler.Commands;

namespace MergeButler.Tests.Commands;

public class PlatformServiceFactoryTests
{
    [Theory]
    [InlineData(Platform.GitHub, PlatformServiceFactory.GitHubTokenEnvironmentVariable)]
    [InlineData(Platform.AzureDevOps, PlatformServiceFactory.AzureDevOpsTokenEnvironmentVariable)]
    public void GetTokenEnvironmentVariableName_ReturnsPrefixedName(Platform platform, string expected)
    {
        string result = PlatformServiceFactory.GetTokenEnvironmentVariableName(platform);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("GitHub", Platform.GitHub)]
    [InlineData("github", Platform.GitHub)]
    [InlineData("GITHUB", Platform.GitHub)]
    [InlineData("AzureDevOps", Platform.AzureDevOps)]
    [InlineData("azuredevops", Platform.AzureDevOps)]
    [InlineData("azdo", Platform.AzureDevOps)]
    [InlineData("AZDO", Platform.AzureDevOps)]
    [InlineData("Azdo", Platform.AzureDevOps)]
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

    [Theory]
    [InlineData(Platform.GitHub, PlatformServiceFactory.GitHubTokenEnvironmentVariable, "GITHUB_TOKEN")]
    [InlineData(Platform.AzureDevOps, PlatformServiceFactory.AzureDevOpsTokenEnvironmentVariable, "AZURE_DEVOPS_TOKEN")]
    public void ResolveToken_NullToken_FallsBackToPrefixedEnvVar(Platform platform, string prefixedEnvVar, string legacyEnvVar)
    {
        string? savedPrefixedToken = Environment.GetEnvironmentVariable(prefixedEnvVar);
        string? savedLegacyToken = Environment.GetEnvironmentVariable(legacyEnvVar);

        try
        {
            Environment.SetEnvironmentVariable(prefixedEnvVar, "prefixed-token");
            Environment.SetEnvironmentVariable(legacyEnvVar, "legacy-token");

            string? result = PlatformServiceFactory.ResolveToken(platform, null);

            Assert.Equal("prefixed-token", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(prefixedEnvVar, savedPrefixedToken);
            Environment.SetEnvironmentVariable(legacyEnvVar, savedLegacyToken);
        }
    }

    [Theory]
    [InlineData(Platform.GitHub, PlatformServiceFactory.GitHubTokenEnvironmentVariable, "GITHUB_TOKEN")]
    [InlineData(Platform.AzureDevOps, PlatformServiceFactory.AzureDevOpsTokenEnvironmentVariable, "AZURE_DEVOPS_TOKEN")]
    public void ResolveToken_NullToken_DoesNotUseLegacyEnvVar(Platform platform, string prefixedEnvVar, string legacyEnvVar)
    {
        string? savedPrefixedToken = Environment.GetEnvironmentVariable(prefixedEnvVar);
        string? savedLegacyToken = Environment.GetEnvironmentVariable(legacyEnvVar);

        try
        {
            Environment.SetEnvironmentVariable(prefixedEnvVar, null);
            Environment.SetEnvironmentVariable(legacyEnvVar, "legacy-token");

            string? result = PlatformServiceFactory.ResolveToken(platform, null);

            Assert.Null(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(prefixedEnvVar, savedPrefixedToken);
            Environment.SetEnvironmentVariable(legacyEnvVar, savedLegacyToken);
        }
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

    [Theory]
    [InlineData("origin\thttps://github.com/owner/repo.git (fetch)\norigin\thttps://github.com/owner/repo.git (push)\n", Platform.GitHub)]
    [InlineData("origin\tgit@github.com:owner/repo.git (fetch)\norigin\tgit@github.com:owner/repo.git (push)\n", Platform.GitHub)]
    [InlineData("origin\thttps://dev.azure.com/org/project/_git/repo (fetch)\n", Platform.AzureDevOps)]
    [InlineData("origin\tgit@ssh.dev.azure.com:v3/org/project/repo (fetch)\n", Platform.AzureDevOps)]
    [InlineData("origin\thttps://org.visualstudio.com/project/_git/repo (fetch)\n", Platform.AzureDevOps)]
    public void InferPlatformFromRemoteOutput_KnownPlatforms_ReturnsPlatform(string remoteOutput, Platform expected)
    {
        Platform? result = PlatformServiceFactory.InferPlatformFromRemoteOutput(remoteOutput);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("origin\thttps://gitlab.com/owner/repo.git (fetch)\n")]
    [InlineData("origin\thttps://bitbucket.org/owner/repo.git (fetch)\n")]
    public void InferPlatformFromRemoteOutput_UnknownPlatforms_ReturnsNull(string remoteOutput)
    {
        Platform? result = PlatformServiceFactory.InferPlatformFromRemoteOutput(remoteOutput);
        Assert.Null(result);
    }
}
