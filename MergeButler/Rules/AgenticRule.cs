using MergeButler.Config;
using MergeButler.PullRequests;

namespace MergeButler.Rules;

public sealed class AgenticRule : IRule
{
    private readonly RuleConfig _config;
    private readonly IPromptEvaluator _promptEvaluator;

    public AgenticRule(RuleConfig config, IPromptEvaluator promptEvaluator)
    {
        if (config.Type != RuleType.Agentic)
        {
            throw new ArgumentException($"Expected Agentic rule type, got {config.Type}.", nameof(config));
        }

        _config = config;
        _promptEvaluator = promptEvaluator;
    }

    public string Name => _config.Name;

    public async Task<RuleResult> EvaluateAsync(PullRequestInfo pullRequest, CancellationToken cancellationToken = default)
    {
        string prompt = $"""
            ## Rule
            {_config.Prompt}

            ## PR Title
            {pullRequest.Title}

            ## PR Description
            {pullRequest.Description}

            ## Changed Files
            {string.Join("\n", pullRequest.ChangedFiles)}

            ## Diff
            {pullRequest.Diff}
            """;

        string responseText = await _promptEvaluator.EvaluatePromptAsync(prompt, cancellationToken);

        string firstLine = responseText.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;

        bool approved = firstLine.Equals("APPROVE", StringComparison.OrdinalIgnoreCase);
        string reason = string.Join('\n', responseText.Split('\n').Skip(1)).Trim();

        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = approved ? "Copilot approved the PR." : "Copilot rejected the PR.";
        }

        return new RuleResult(approved, Name, reason);
    }
}
