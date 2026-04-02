using MergeButler.Config;
using MergeButler.PullRequests;
using MergeButler.Rules;

namespace MergeButler.Tests.Rules;

public class AgenticRuleTests
{
    private static PullRequestInfo CreatePr() =>
        new()
        {
            Title = "Update packages",
            Description = "Bump NuGet versions",
            ChangedFiles = ["Directory.Packages.props"],
            Diff = "-Version=\"1.0.0\"\n+Version=\"2.0.0\""
        };

    [Fact]
    public async Task EvaluateAsync_CopilotApproves_ReturnsApproved()
    {
        Mock<IPromptEvaluator> mockEvaluator = new();
        mockEvaluator.Setup(e => e.EvaluatePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("APPROVE\nLooks like a safe package update.");

        RuleConfig config = new()
        {
            Name = "Safe updates",
            Type = RuleType.Agentic,
            Prompt = "Approve if only package versions changed."
        };
        AgenticRule rule = new(config, mockEvaluator.Object);

        RuleResult result = await rule.EvaluateAsync(CreatePr(), TestContext.Current.CancellationToken);

        Assert.True(result.Approved);
        Assert.Contains("safe package update", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_CopilotRejects_ReturnsRejected()
    {
        Mock<IPromptEvaluator> mockEvaluator = new();
        mockEvaluator.Setup(e => e.EvaluatePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("REJECT\nThis changes application logic.");

        RuleConfig config = new()
        {
            Name = "Safe updates",
            Type = RuleType.Agentic,
            Prompt = "Approve if only package versions changed."
        };
        AgenticRule rule = new(config, mockEvaluator.Object);

        RuleResult result = await rule.EvaluateAsync(CreatePr(), TestContext.Current.CancellationToken);

        Assert.False(result.Approved);
        Assert.Contains("application logic", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_CopilotReturnsOnlyApprove_UsesDefaultReason()
    {
        Mock<IPromptEvaluator> mockEvaluator = new();
        mockEvaluator.Setup(e => e.EvaluatePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("APPROVE");

        RuleConfig config = new()
        {
            Name = "Test",
            Type = RuleType.Agentic,
            Prompt = "Test prompt"
        };
        AgenticRule rule = new(config, mockEvaluator.Object);

        RuleResult result = await rule.EvaluateAsync(CreatePr(), TestContext.Current.CancellationToken);

        Assert.True(result.Approved);
        Assert.Equal("Copilot approved the PR.", result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_PromptContainsPrInfo()
    {
        string? capturedPrompt = null;
        Mock<IPromptEvaluator> mockEvaluator = new();
        mockEvaluator.Setup(e => e.EvaluatePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((prompt, _) => capturedPrompt = prompt)
            .ReturnsAsync("REJECT\nNo.");

        RuleConfig config = new()
        {
            Name = "Test",
            Type = RuleType.Agentic,
            Prompt = "Check this PR"
        };
        AgenticRule rule = new(config, mockEvaluator.Object);

        await rule.EvaluateAsync(CreatePr(), TestContext.Current.CancellationToken);

        Assert.NotNull(capturedPrompt);
        Assert.Contains("Update packages", capturedPrompt);
        Assert.Contains("Directory.Packages.props", capturedPrompt);
        Assert.Contains("Check this PR", capturedPrompt);
    }

    [Fact]
    public void Constructor_WrongRuleType_Throws()
    {
        Mock<IPromptEvaluator> mockEvaluator = new();
        RuleConfig config = new()
        {
            Name = "Wrong",
            Type = RuleType.FileGlob,
            Patterns = ["**/*.md"]
        };

        Assert.Throws<ArgumentException>(() => new AgenticRule(config, mockEvaluator.Object));
    }
}
