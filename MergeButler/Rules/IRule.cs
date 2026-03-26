using MergeButler.PullRequests;

namespace MergeButler.Rules;

public interface IRule
{
    string Name { get; }
    Task<RuleResult> EvaluateAsync(PullRequestInfo pullRequest, CancellationToken cancellationToken = default);
}
