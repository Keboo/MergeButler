using System.Text.RegularExpressions;
using Octokit;

namespace MergeButler.PullRequests;

public sealed partial class GitHubPullRequestService : IPullRequestService
{
    private readonly IGitHubClient _gitHubClient;

    public GitHubPullRequestService(IGitHubClient gitHubClient)
    {
        _gitHubClient = gitHubClient;
    }

    public async Task<PullRequestInfo> GetPullRequestAsync(string pullRequestUrl, CancellationToken cancellationToken = default)
    {
        (string owner, string repo, int number) = ParsePullRequestUrl(pullRequestUrl);

        PullRequest pr = await _gitHubClient.PullRequest.Get(owner, repo, number);
        IReadOnlyList<PullRequestFile> files = await _gitHubClient.PullRequest.Files(owner, repo, number);

        // Build combined diff from individual file patches
        string diff = string.Join("\n", files
            .Where(f => f.Patch is not null)
            .Select(f => $"--- a/{f.FileName}\n+++ b/{f.FileName}\n{f.Patch}"));

        return new PullRequestInfo
        {
            Title = pr.Title,
            Description = pr.Body ?? string.Empty,
            ChangedFiles = files.Select(f => f.FileName).ToList(),
            Diff = diff
        };
    }

    public async Task ApproveAsync(string pullRequestUrl, CancellationToken cancellationToken = default)
    {
        (string owner, string repo, int number) = ParsePullRequestUrl(pullRequestUrl);

        PullRequestReviewCreate review = new()
        {
            Body = "Automatically approved by MergeButler.",
            Event = PullRequestReviewEvent.Approve
        };

        await _gitHubClient.PullRequest.Review.Create(owner, repo, number, review);
    }

    public static (string Owner, string Repo, int Number) ParsePullRequestUrl(string url)
    {
        // Supports: https://github.com/{owner}/{repo}/pull/{number}
        Match match = GitHubPrUrlPattern().Match(url);
        if (!match.Success)
        {
            throw new ArgumentException($"Invalid GitHub PR URL: {url}", nameof(url));
        }

        return (match.Groups["owner"].Value, match.Groups["repo"].Value, int.Parse(match.Groups["number"].Value));
    }

    [GeneratedRegex(@"github\.com/(?<owner>[^/]+)/(?<repo>[^/]+)/pull/(?<number>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubPrUrlPattern();
}
