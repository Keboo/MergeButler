using Microsoft.Extensions.FileSystemGlobbing;
using MergeButler.Config;
using MergeButler.PullRequests;

namespace MergeButler.Rules;

public sealed class FileGlobRule : IRule
{
    private readonly RuleConfig _config;

    public FileGlobRule(RuleConfig config)
    {
        if (config.Type != RuleType.FileGlob)
        {
            throw new ArgumentException($"Expected FileGlob rule type, got {config.Type}.", nameof(config));
        }

        _config = config;
    }

    public string Name => _config.Name;

    public Task<RuleResult> EvaluateAsync(PullRequestInfo pullRequest, CancellationToken cancellationToken = default)
    {
        Matcher matcher = new();
        foreach (string pattern in _config.Patterns)
        {
            matcher.AddInclude(pattern);
        }

        // Strict mode: ALL changed files must match the glob patterns
        bool allFilesMatch = pullRequest.ChangedFiles.Count > 0
            && pullRequest.ChangedFiles.All(file =>
            {
                PatternMatchingResult result = matcher.Match(file);
                return result.HasMatches;
            });

        RuleResult ruleResult = allFilesMatch
            ? new RuleResult(true, Name, "All changed files match the configured glob patterns.")
            : new RuleResult(false, Name, "One or more changed files do not match the configured glob patterns.");

        return Task.FromResult(ruleResult);
    }
}
