using System.Diagnostics;
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
        if (string.Equals(platformStr, "azdo", StringComparison.OrdinalIgnoreCase))
        {
            return Platform.AzureDevOps;
        }

        if (Enum.TryParse<Platform>(platformStr, ignoreCase: true, out Platform platform))
        {
            return platform;
        }

        throw new ArgumentException(
            $"Invalid platform '{platformStr}'. Supported values: GitHub, AzureDevOps (alias: azdo).",
            nameof(platformStr));
    }

    /// <summary>
    /// Detects the hosting platform by inspecting git remote URLs in the working directory.
    /// </summary>
    public static Platform? DetectPlatformFromGitRemotes(string? workingDirectory = null)
    {
        try
        {
            ProcessStartInfo psi = new("git", ["remote", "-v"])
            {
                WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process is null) return null;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0) return null;

            return InferPlatformFromRemoteOutput(output);
        }
        catch
        {
            return null;
        }
    }

    public static Platform? InferPlatformFromRemoteOutput(string remoteOutput)
    {
        if (remoteOutput.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            return Platform.GitHub;
        if (remoteOutput.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase) ||
            remoteOutput.Contains("visualstudio.com", StringComparison.OrdinalIgnoreCase))
            return Platform.AzureDevOps;
        return null;
    }
}
