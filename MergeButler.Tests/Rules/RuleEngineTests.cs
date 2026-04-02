using MergeButler.Config;
using MergeButler.PullRequests;
using MergeButler.Rules;

namespace MergeButler.Tests.Rules;

public class RuleEngineTests
{
    private static PullRequestInfo CreatePr(
        string title = "Test PR",
        string description = "Test description",
        string[]? files = null) =>
        new()
        {
            Title = title,
            Description = description,
            ChangedFiles = files ?? ["file.txt"],
            Diff = "some diff"
        };

    [Fact]
    public async Task EvaluateAsync_ExclusionMatches_ReturnsExcluded()
    {
        ExclusionEvaluator exclusionEvaluator = new();
        RuleEngine engine = new(exclusionEvaluator, []);

        List<ExclusionConfig> exclusions =
        [
            new() { Pattern = "DO NOT AUTO-APPROVE", Target = ExclusionTarget.Title }
        ];

        EvaluationResult result = await engine.EvaluateAsync(
            CreatePr(title: "DO NOT AUTO-APPROVE this PR"),
            exclusions,
            TestContext.Current.CancellationToken);

        Assert.False(result.Approved);
        Assert.True(result.Excluded);
        Assert.Contains("DO NOT AUTO-APPROVE", result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_RuleApproves_ReturnsApproved()
    {
        ExclusionEvaluator exclusionEvaluator = new();
        Mock<IRule> mockRule = new();
        mockRule.Setup(r => r.EvaluateAsync(It.IsAny<PullRequestInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleResult(true, "Test Rule", "Files matched"));

        RuleEngine engine = new(exclusionEvaluator, [mockRule.Object]);

        EvaluationResult result = await engine.EvaluateAsync(CreatePr(), [], TestContext.Current.CancellationToken);

        Assert.True(result.Approved);
        Assert.False(result.Excluded);
        Assert.Equal("Test Rule", result.MatchedRule);
    }

    [Fact]
    public async Task EvaluateAsync_NoRulesMatch_ReturnsNotApproved()
    {
        ExclusionEvaluator exclusionEvaluator = new();
        Mock<IRule> mockRule = new();
        mockRule.Setup(r => r.EvaluateAsync(It.IsAny<PullRequestInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleResult(false, "Test Rule", "No match"));

        RuleEngine engine = new(exclusionEvaluator, [mockRule.Object]);

        EvaluationResult result = await engine.EvaluateAsync(CreatePr(), [], TestContext.Current.CancellationToken);

        Assert.False(result.Approved);
        Assert.False(result.Excluded);
        Assert.Null(result.MatchedRule);
    }

    [Fact]
    public async Task EvaluateAsync_OrLogic_FirstMatchWins()
    {
        ExclusionEvaluator exclusionEvaluator = new();

        Mock<IRule> failRule = new();
        failRule.Setup(r => r.EvaluateAsync(It.IsAny<PullRequestInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleResult(false, "Fail Rule", "No match"));

        Mock<IRule> passRule = new();
        passRule.Setup(r => r.EvaluateAsync(It.IsAny<PullRequestInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleResult(true, "Pass Rule", "Matched!"));

        Mock<IRule> neverCalled = new();

        RuleEngine engine = new(exclusionEvaluator, [failRule.Object, passRule.Object, neverCalled.Object]);

        EvaluationResult result = await engine.EvaluateAsync(CreatePr(), [], TestContext.Current.CancellationToken);

        Assert.True(result.Approved);
        Assert.Equal("Pass Rule", result.MatchedRule);
        neverCalled.Verify(r => r.EvaluateAsync(It.IsAny<PullRequestInfo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_ExclusionTakesPrecedenceOverRules()
    {
        ExclusionEvaluator exclusionEvaluator = new();

        Mock<IRule> mockRule = new();
        mockRule.Setup(r => r.EvaluateAsync(It.IsAny<PullRequestInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleResult(true, "Would Approve", "Matched"));

        RuleEngine engine = new(exclusionEvaluator, [mockRule.Object]);

        List<ExclusionConfig> exclusions =
        [
            new() { Pattern = "SKIP", Target = ExclusionTarget.Title }
        ];

        EvaluationResult result = await engine.EvaluateAsync(
            CreatePr(title: "SKIP this PR"),
            exclusions,
            TestContext.Current.CancellationToken);

        Assert.False(result.Approved);
        Assert.True(result.Excluded);
        mockRule.Verify(r => r.EvaluateAsync(It.IsAny<PullRequestInfo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_NoRulesNoExclusions_ReturnsNotApproved()
    {
        ExclusionEvaluator exclusionEvaluator = new();
        RuleEngine engine = new(exclusionEvaluator, []);

        EvaluationResult result = await engine.EvaluateAsync(CreatePr(), [], TestContext.Current.CancellationToken);

        Assert.False(result.Approved);
        Assert.False(result.Excluded);
    }
}
