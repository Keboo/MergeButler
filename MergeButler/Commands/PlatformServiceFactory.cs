using MergeButler.PullRequests;
using Octokit;

namespace MergeButler.Commands;

/// <summary>
/// Shared logic for creating platform-specific PR services.
/// </summary>
public static class PlatformServiceFactory
{
    public static string? ResolveToken(Platform platform, string? providedToken) =>
        providedToken ?? platform switch
        {
            Platform.GitHub => Environment.GetEnvironmentVariable("GITHUB_TOKEN"),
            Platform.AzureDevOps => Environment.GetEnvironmentVariable("AZURE_DEVOPS_TOKEN"),
            _ => null
        };

    public static IPullRequestService CreateService(Platform platform, string token)
    {
        return platform switch
        {
            Platform.GitHub => CreateGitHubService(token),
            Platform.AzureDevOps => CreateAzureDevOpsService(token),
            _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
        };
    }

    private static GitHubPullRequestService CreateGitHubService(string token)
    {
        GitHubClient ghClient = new(new ProductHeaderValue("MergeButler"))
        {
            Credentials = new Credentials(token)
        };
        return new(ghClient);
    }

    private static AzureDevOpsPullRequestService CreateAzureDevOpsService(string token)
    {
        HttpClient httpClient = new();
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{token}")));
        return new(httpClient);
    }

    public static Platform ParsePlatform(string platformStr)
    {
        if (Enum.TryParse<Platform>(platformStr, ignoreCase: true, out Platform platform))
        {
            return platform;
        }

        throw new ArgumentException(
            $"Invalid platform '{platformStr}'. Supported values: GitHub, AzureDevOps.",
            nameof(platformStr));
    }
}
