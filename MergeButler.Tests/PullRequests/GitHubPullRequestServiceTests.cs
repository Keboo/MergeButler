using MergeButler.PullRequests;

namespace MergeButler.Tests.PullRequests;

public class GitHubPullRequestServiceTests
{
    [Theory]
    [InlineData("https://github.com/owner/repo/pull/42", "owner", "repo", 42)]
    [InlineData("https://github.com/my-org/my-repo/pull/1", "my-org", "my-repo", 1)]
    [InlineData("https://github.com/Microsoft/TypeScript/pull/12345", "Microsoft", "TypeScript", 12345)]
    public void ParsePullRequestUrl_ValidUrls_ParsesCorrectly(string url, string owner, string repo, int number)
    {
        (string parsedOwner, string parsedRepo, int parsedNumber) = GitHubPullRequestService.ParsePullRequestUrl(url);

        Assert.Equal(owner, parsedOwner);
        Assert.Equal(repo, parsedRepo);
        Assert.Equal(number, parsedNumber);
    }

    [Theory]
    [InlineData("https://github.com/owner/repo")]
    [InlineData("https://dev.azure.com/org/project/_git/repo/pullrequest/1")]
    [InlineData("not-a-url")]
    public void ParsePullRequestUrl_InvalidUrls_ThrowsArgumentException(string url)
    {
        Assert.Throws<ArgumentException>(() => GitHubPullRequestService.ParsePullRequestUrl(url));
    }
}
