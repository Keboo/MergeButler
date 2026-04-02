using MergeButler.Commands;
using MergeButler.PullRequests;

namespace MergeButler.Tests.PullRequests;

public class PullRequestUrlResolverTests
{
    [Theory]
    [InlineData("https://dev.azure.com/myorg/myproject/_git/myrepo/pullrequest/42")]
    [InlineData("https://github.com/owner/repo/pull/42")]
    public void Resolve_WithUrl_ReturnsAsIs(string url)
    {
        string result = PullRequestUrlResolver.Resolve(url, Platform.GitHub, null);
        Assert.Equal(url, result);
    }

    [Theory]
    [InlineData("https://dev.azure.com/myorg/myproject/_git/myrepo", 42,
        "https://dev.azure.com/myorg/myproject/_git/myrepo/pullrequest/42")]
    [InlineData("https://dev.azure.com/myorg/myproject/_git/myrepo.git", 1,
        "https://dev.azure.com/myorg/myproject/_git/myrepo/pullrequest/1")]
    [InlineData("git@ssh.dev.azure.com:v3/myorg/myproject/myrepo", 99,
        "https://dev.azure.com/myorg/myproject/_git/myrepo/pullrequest/99")]
    [InlineData("https://myorg.visualstudio.com/myproject/_git/myrepo", 7,
        "https://dev.azure.com/myorg/myproject/_git/myrepo/pullrequest/7")]
    public void Resolve_WithNumber_AzureDevOps_BuildsCorrectUrl(
        string remoteUrl, int prNumber, string expected)
    {
        string result = PullRequestUrlResolver.Resolve(
            prNumber.ToString(), Platform.AzureDevOps, remoteUrl);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("https://github.com/owner/repo", 42,
        "https://github.com/owner/repo/pull/42")]
    [InlineData("https://github.com/owner/repo.git", 1,
        "https://github.com/owner/repo/pull/1")]
    [InlineData("git@github.com:owner/repo.git", 99,
        "https://github.com/owner/repo/pull/99")]
    [InlineData("git@github.com:owner/repo", 7,
        "https://github.com/owner/repo/pull/7")]
    public void Resolve_WithNumber_GitHub_BuildsCorrectUrl(
        string remoteUrl, int prNumber, string expected)
    {
        string result = PullRequestUrlResolver.Resolve(
            prNumber.ToString(), Platform.GitHub, remoteUrl);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("not-a-url-or-number")]
    [InlineData("-1")]
    [InlineData("0")]
    public void Resolve_WithInvalidReference_ThrowsArgumentException(string prReference)
    {
        Assert.Throws<ArgumentException>(() =>
            PullRequestUrlResolver.Resolve(
                prReference, Platform.GitHub, "https://github.com/owner/repo"));
    }

    [Fact]
    public void Resolve_WithNumberButNoRemoteUrl_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PullRequestUrlResolver.Resolve("42", Platform.GitHub, null));
    }

    [Fact]
    public void Resolve_AzureDevOps_WithUnrecognizedRemote_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            PullRequestUrlResolver.Resolve(
                "42", Platform.AzureDevOps, "https://example.com/some/repo"));
    }

    [Fact]
    public void Resolve_GitHub_WithUnrecognizedRemote_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            PullRequestUrlResolver.Resolve(
                "42", Platform.GitHub, "https://example.com/some/repo"));
    }
}
