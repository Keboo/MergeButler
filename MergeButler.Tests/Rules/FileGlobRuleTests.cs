using MergeButler.Config;
using MergeButler.PullRequests;
using MergeButler.Rules;

namespace MergeButler.Tests.Rules;

public class FileGlobRuleTests
{
    private static PullRequestInfo CreatePr(params string[] files) =>
        new()
        {
            Title = "Test PR",
            Description = "Test",
            ChangedFiles = files,
            Diff = "some diff"
        };

    [Fact]
    public async Task EvaluateAsync_AllFilesMatchSingleGlob_Approves()
    {
        RuleConfig config = new()
        {
            Name = "Docs",
            Type = RuleType.FileGlob,
            Patterns = ["**/*.md"]
        };
        FileGlobRule rule = new(config);

        RuleResult result = await rule.EvaluateAsync(CreatePr("README.md", "docs/guide.md"), TestContext.Current.CancellationToken);

        Assert.True(result.Approved);
    }

    [Fact]
    public async Task EvaluateAsync_AllFilesMatchMultipleGlobs_Approves()
    {
        RuleConfig config = new()
        {
            Name = "Docs and config",
            Type = RuleType.FileGlob,
            Patterns = ["**/*.md", "**/*.yml"]
        };
        FileGlobRule rule = new(config);

        RuleResult result = await rule.EvaluateAsync(CreatePr("README.md", ".github/dependabot.yml"), TestContext.Current.CancellationToken);

        Assert.True(result.Approved);
    }

    [Fact]
    public async Task EvaluateAsync_SomeFilesDontMatch_Rejects()
    {
        RuleConfig config = new()
        {
            Name = "Docs",
            Type = RuleType.FileGlob,
            Patterns = ["**/*.md"]
        };
        FileGlobRule rule = new(config);

        RuleResult result = await rule.EvaluateAsync(CreatePr("README.md", "src/Program.cs"), TestContext.Current.CancellationToken);

        Assert.False(result.Approved);
    }

    [Fact]
    public async Task EvaluateAsync_NoFiles_Rejects()
    {
        RuleConfig config = new()
        {
            Name = "Docs",
            Type = RuleType.FileGlob,
            Patterns = ["**/*.md"]
        };
        FileGlobRule rule = new(config);

        RuleResult result = await rule.EvaluateAsync(CreatePr(), TestContext.Current.CancellationToken);

        Assert.False(result.Approved);
    }

    [Fact]
    public async Task EvaluateAsync_NestedDirectoryGlob_Approves()
    {
        RuleConfig config = new()
        {
            Name = "Docs folder",
            Type = RuleType.FileGlob,
            Patterns = ["docs/**"]
        };
        FileGlobRule rule = new(config);

        RuleResult result = await rule.EvaluateAsync(CreatePr("docs/readme.md", "docs/api/reference.md"), TestContext.Current.CancellationToken);

        Assert.True(result.Approved);
    }

    [Fact]
    public void Constructor_WrongRuleType_Throws()
    {
        RuleConfig config = new()
        {
            Name = "Wrong",
            Type = RuleType.Agentic,
            Prompt = "test"
        };

        Assert.Throws<ArgumentException>(() => new FileGlobRule(config));
    }

    [Fact]
    public async Task EvaluateAsync_RuleName_IsSet()
    {
        RuleConfig config = new()
        {
            Name = "My Rule",
            Type = RuleType.FileGlob,
            Patterns = ["**/*.md"]
        };
        FileGlobRule rule = new(config);

        Assert.Equal("My Rule", rule.Name);
        RuleResult result = await rule.EvaluateAsync(CreatePr("file.md"), TestContext.Current.CancellationToken);
        Assert.Equal("My Rule", result.RuleName);
    }
}
