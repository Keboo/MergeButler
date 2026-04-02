using System.Diagnostics;
using System.Text.RegularExpressions;
using MergeButler.Commands;

namespace MergeButler.PullRequests;

/// <summary>
/// Resolves a pull request reference (URL or number) to a full PR URL.
/// When a PR number is provided, the repository URL is inferred from the git remote.
/// </summary>
public static partial class PullRequestUrlResolver
{
    /// <summary>
    /// Resolves a PR reference to a full URL. If already a URL, returns as-is.
    /// If a number, builds the URL from the provided git remote URL and platform.
    /// </summary>
    public static string Resolve(string prReference, Platform platform, string? remoteUrl)
    {
        if (Uri.TryCreate(prReference, UriKind.Absolute, out Uri? uri) &&
            uri.Scheme is "http" or "https")
        {
            return prReference;
        }

        if (!int.TryParse(prReference, out int prNumber) || prNumber <= 0)
        {
            throw new ArgumentException(
                $"'{prReference}' is not a valid pull request URL or number.", nameof(prReference));
        }

        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            throw new InvalidOperationException(
                "A git remote URL is required when using a PR number. " +
                "Provide a full PR URL, or run from within a git repository.");
        }

        return platform switch
        {
            Platform.AzureDevOps => BuildAzureDevOpsUrl(remoteUrl, prNumber),
            Platform.GitHub => BuildGitHubUrl(remoteUrl, prNumber),
            _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
        };
    }

    /// <summary>
    /// Resolves a PR reference, auto-detecting the git remote URL when a number is provided.
    /// </summary>
    public static async Task<string> ResolveFromGitRemoteAsync(string prReference, Platform platform)
    {
        if (Uri.TryCreate(prReference, UriKind.Absolute, out Uri? uri) &&
            uri.Scheme is "http" or "https")
        {
            return prReference;
        }

        string remoteUrl = await GetGitRemoteUrlAsync();
        return Resolve(prReference, platform, remoteUrl);
    }

    internal static string BuildAzureDevOpsUrl(string remoteUrl, int prNumber)
    {
        // HTTPS: https://dev.azure.com/{org}/{project}/_git/{repo}
        Match match = AzDoHttpsRemotePattern().Match(remoteUrl);
        if (match.Success)
        {
            return $"https://dev.azure.com/{match.Groups["org"].Value}/{match.Groups["project"].Value}/_git/{match.Groups["repo"].Value}/pullrequest/{prNumber}";
        }

        // SSH: git@ssh.dev.azure.com:v3/{org}/{project}/{repo}
        match = AzDoSshRemotePattern().Match(remoteUrl);
        if (match.Success)
        {
            return $"https://dev.azure.com/{match.Groups["org"].Value}/{match.Groups["project"].Value}/_git/{match.Groups["repo"].Value}/pullrequest/{prNumber}";
        }

        // Legacy: https://{org}.visualstudio.com/{project}/_git/{repo}
        match = AzDoVstsRemotePattern().Match(remoteUrl);
        if (match.Success)
        {
            return $"https://dev.azure.com/{match.Groups["org"].Value}/{match.Groups["project"].Value}/_git/{match.Groups["repo"].Value}/pullrequest/{prNumber}";
        }

        throw new ArgumentException(
            $"Could not parse Azure DevOps repository URL from remote: {remoteUrl}",
            nameof(remoteUrl));
    }

    internal static string BuildGitHubUrl(string remoteUrl, int prNumber)
    {
        // HTTPS: https://github.com/{owner}/{repo}(.git)?
        Match match = GitHubHttpsRemotePattern().Match(remoteUrl);
        if (match.Success)
        {
            return $"https://github.com/{match.Groups["owner"].Value}/{match.Groups["repo"].Value}/pull/{prNumber}";
        }

        // SSH: git@github.com:{owner}/{repo}(.git)?
        match = GitHubSshRemotePattern().Match(remoteUrl);
        if (match.Success)
        {
            return $"https://github.com/{match.Groups["owner"].Value}/{match.Groups["repo"].Value}/pull/{prNumber}";
        }

        throw new ArgumentException(
            $"Could not parse GitHub repository URL from remote: {remoteUrl}",
            nameof(remoteUrl));
    }

    private static async Task<string> GetGitRemoteUrlAsync()
    {
        ProcessStartInfo psi = new("git", "remote get-url origin")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        string output = (await process.StandardOutput.ReadToEndAsync()).Trim();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "Could not determine repository URL from git remote 'origin'. " +
                "Provide a full PR URL, or ensure you are in a git repository with an 'origin' remote.");
        }

        return output;
    }

    [GeneratedRegex(@"dev\.azure\.com/(?<org>[^/]+)/(?<project>[^/]+)/_git/(?<repo>[^/]+?)(?:\.git)?$", RegexOptions.IgnoreCase)]
    private static partial Regex AzDoHttpsRemotePattern();

    [GeneratedRegex(@"ssh\.dev\.azure\.com:v3/(?<org>[^/]+)/(?<project>[^/]+)/(?<repo>[^/]+?)(?:\.git)?$", RegexOptions.IgnoreCase)]
    private static partial Regex AzDoSshRemotePattern();

    [GeneratedRegex(@"(?<org>[^/.]+)\.visualstudio\.com/(?<project>[^/]+)/_git/(?<repo>[^/]+?)(?:\.git)?$", RegexOptions.IgnoreCase)]
    private static partial Regex AzDoVstsRemotePattern();

    [GeneratedRegex(@"github\.com/(?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?$", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubHttpsRemotePattern();

    [GeneratedRegex(@"github\.com:(?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?$", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubSshRemotePattern();
}
