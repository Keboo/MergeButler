using MergeButler.PullRequests;

namespace MergeButler.Tests.PullRequests;

public class AzureDevOpsPullRequestServiceTests
{
    [Theory]
    [InlineData("https://dev.azure.com/myorg/myproject/_git/myrepo/pullrequest/42", "myorg", "myproject", "myrepo", 42)]
    [InlineData("https://dev.azure.com/org/proj/_git/repo/pullrequest/1", "org", "proj", "repo", 1)]
    public void ParsePullRequestUrl_ValidUrls_ParsesCorrectly(string url, string org, string project, string repo, int id)
    {
        (string parsedOrg, string parsedProject, string parsedRepo, int parsedId) =
            AzureDevOpsPullRequestService.ParsePullRequestUrl(url);

        Assert.Equal(org, parsedOrg);
        Assert.Equal(project, parsedProject);
        Assert.Equal(repo, parsedRepo);
        Assert.Equal(id, parsedId);
    }

    [Theory]
    [InlineData("https://github.com/owner/repo/pull/42")]
    [InlineData("https://dev.azure.com/org/proj/_git/repo")]
    [InlineData("not-a-url")]
    public void ParsePullRequestUrl_InvalidUrls_ThrowsArgumentException(string url)
    {
        Assert.Throws<ArgumentException>(() => AzureDevOpsPullRequestService.ParsePullRequestUrl(url));
    }
}
