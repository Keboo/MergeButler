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

    public static (IPullRequestProvider Provider, IPullRequestApprover Approver) CreateServices(
        Platform platform, string token)
    {
        return platform switch
        {
            Platform.GitHub => CreateGitHubServices(token),
            Platform.AzureDevOps => CreateAzureDevOpsServices(token),
            _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
        };
    }

    private static (IPullRequestProvider, IPullRequestApprover) CreateGitHubServices(string token)
    {
        GitHubClient ghClient = new(new ProductHeaderValue("MergeButler"))
        {
            Credentials = new Credentials(token)
        };
        GitHubPullRequestService service = new(ghClient);
        return (service, service);
    }

    private static (IPullRequestProvider, IPullRequestApprover) CreateAzureDevOpsServices(string token)
    {
        HttpClient httpClient = new();
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{token}")));
        AzureDevOpsPullRequestService service = new(httpClient);
        return (service, service);
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
