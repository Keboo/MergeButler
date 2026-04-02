using MergeButler.Mcp;

namespace MergeButler.Tests.Mcp;

public class PullRequestToolsTests
{
    [Fact]
    public async Task GradePullRequest_NoToken_ReturnsError()
    {
        // Ensure env var is not set for this test
        string? savedToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);

            string result = await PullRequestTools.GradePullRequest(
                "https://github.com/owner/repo/pull/1",
                "GitHub",
                token: null,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Contains("ERROR", result);
            Assert.Contains("token", result, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", savedToken);
        }
    }

    [Fact]
    public async Task GradePullRequest_InvalidPlatform_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            PullRequestTools.GradePullRequest(
                "https://github.com/owner/repo/pull/1",
                "InvalidPlatform",
                token: "fake-token",
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ApprovePullRequest_NoToken_ReturnsError()
    {
        string? savedToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);

            string result = await PullRequestTools.ApprovePullRequest(
                "https://github.com/owner/repo/pull/1",
                "GitHub",
                token: null,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Contains("ERROR", result);
            Assert.Contains("token", result, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", savedToken);
        }
    }

    [Fact]
    public async Task ApprovePullRequest_InvalidPlatform_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            PullRequestTools.ApprovePullRequest(
                "https://github.com/owner/repo/pull/1",
                "InvalidPlatform",
                token: "fake-token",
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GradePullRequest_MissingConfigFile_ThrowsFileNotFound()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            PullRequestTools.GradePullRequest(
                "https://github.com/owner/repo/pull/1",
                "GitHub",
                configPath: "nonexistent-config.yml",
                token: "fake-token",
                cancellationToken: TestContext.Current.CancellationToken));
    }
}
