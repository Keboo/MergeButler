using MergeButler.Config;
using MergeButler.PullRequests;

namespace MergeButler.Rules;

public sealed class RuleEngine
{
    private readonly ExclusionEvaluator _exclusionEvaluator;
    private readonly IReadOnlyList<IRule> _rules;

    public RuleEngine(ExclusionEvaluator exclusionEvaluator, IReadOnlyList<IRule> rules)
    {
        _exclusionEvaluator = exclusionEvaluator;
        _rules = rules;
    }

    public async Task<EvaluationResult> EvaluateAsync(
        PullRequestInfo pullRequest,
        IReadOnlyList<ExclusionConfig> exclusions,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Check exclusions first
        ExclusionConfig? matchingExclusion = _exclusionEvaluator.GetMatchingExclusion(pullRequest, exclusions);
        if (matchingExclusion is not null)
        {
            return new EvaluationResult(
                Approved: false,
                Excluded: true,
                Reason: $"PR excluded by pattern '{matchingExclusion.Pattern}' matching {matchingExclusion.Target}.",
                MatchedRule: null);
        }

        // Step 2: Evaluate rules with OR logic — first match wins
        foreach (IRule rule in _rules)
        {
            RuleResult result = await rule.EvaluateAsync(pullRequest, cancellationToken);
            if (result.Approved)
            {
                return new EvaluationResult(
                    Approved: true,
                    Excluded: false,
                    Reason: result.Reason,
                    MatchedRule: result.RuleName);
            }
        }

        return new EvaluationResult(
            Approved: false,
            Excluded: false,
            Reason: "No rules matched for approval.",
            MatchedRule: null);
    }
}

public sealed record EvaluationResult(bool Approved, bool Excluded, string Reason, string? MatchedRule);
