using MergeButler.Config;
using MergeButler.PullRequests;
using MergeButler.Rules;

namespace MergeButler.Tests.Rules;

public class ExclusionEvaluatorTests
{
    private readonly ExclusionEvaluator _evaluator = new();

    private static PullRequestInfo CreatePr(string title = "Test PR", string description = "Some description") =>
        new()
        {
            Title = title,
            Description = description,
            ChangedFiles = ["file.txt"],
            Diff = "some diff"
        };

    [Fact]
    public void IsExcluded_MatchesTitle_ReturnsTrue()
    {
        PullRequestInfo pr = CreatePr(title: "DO NOT AUTO-APPROVE this");
        List<ExclusionConfig> exclusions =
        [
            new() { Pattern = "DO NOT AUTO-APPROVE", Target = ExclusionTarget.Title }
        ];

        Assert.True(_evaluator.IsExcluded(pr, exclusions));
    }

    [Fact]
    public void IsExcluded_MatchesDescription_ReturnsTrue()
    {
        PullRequestInfo pr = CreatePr(description: "This needs [manual review]");
        List<ExclusionConfig> exclusions =
        [
            new() { Pattern = @"\[manual review\]", Target = ExclusionTarget.Description }
        ];

        Assert.True(_evaluator.IsExcluded(pr, exclusions));
    }

    [Fact]
    public void IsExcluded_BothTarget_MatchesTitle_ReturnsTrue()
    {
        PullRequestInfo pr = CreatePr(title: "[manual review] something");
        List<ExclusionConfig> exclusions =
        [
            new() { Pattern = @"\[manual review\]", Target = ExclusionTarget.Both }
        ];

        Assert.True(_evaluator.IsExcluded(pr, exclusions));
    }

    [Fact]
    public void IsExcluded_BothTarget_MatchesDescription_ReturnsTrue()
    {
        PullRequestInfo pr = CreatePr(description: "[manual review] details");
        List<ExclusionConfig> exclusions =
        [
            new() { Pattern = @"\[manual review\]", Target = ExclusionTarget.Both }
        ];

        Assert.True(_evaluator.IsExcluded(pr, exclusions));
    }

    [Fact]
    public void IsExcluded_NoMatch_ReturnsFalse()
    {
        PullRequestInfo pr = CreatePr(title: "Normal PR", description: "Normal description");
        List<ExclusionConfig> exclusions =
        [
            new() { Pattern = "SKIP_THIS", Target = ExclusionTarget.Both }
        ];

        Assert.False(_evaluator.IsExcluded(pr, exclusions));
    }

    [Fact]
    public void IsExcluded_EmptyExclusions_ReturnsFalse()
    {
        PullRequestInfo pr = CreatePr();
        Assert.False(_evaluator.IsExcluded(pr, []));
    }

    [Fact]
    public void IsExcluded_CaseInsensitive_ReturnsTrue()
    {
        PullRequestInfo pr = CreatePr(title: "do not auto-approve this");
        List<ExclusionConfig> exclusions =
        [
            new() { Pattern = "DO NOT AUTO-APPROVE", Target = ExclusionTarget.Title }
        ];

        Assert.True(_evaluator.IsExcluded(pr, exclusions));
    }

    [Fact]
    public void GetMatchingExclusion_ReturnsMatchingExclusion()
    {
        PullRequestInfo pr = CreatePr(title: "DO NOT AUTO-APPROVE");
        ExclusionConfig exclusion = new() { Pattern = "DO NOT AUTO-APPROVE", Target = ExclusionTarget.Title };

        ExclusionConfig? result = _evaluator.GetMatchingExclusion(pr, [exclusion]);

        Assert.Same(exclusion, result);
    }

    [Fact]
    public void GetMatchingExclusion_NoMatch_ReturnsNull()
    {
        PullRequestInfo pr = CreatePr();
        ExclusionConfig? result = _evaluator.GetMatchingExclusion(pr, [new() { Pattern = "NOPE", Target = ExclusionTarget.Title }]);

        Assert.Null(result);
    }
}
