using System.ComponentModel;
using System.Text;
using MergeButler.Commands;
using MergeButler.Config;
using MergeButler.PullRequests;
using MergeButler.Rules;
using ModelContextProtocol.Server;

namespace MergeButler.Mcp;

[McpServerToolType]
public class PullRequestTools
{
    [McpServerTool(Name = "grade_pull_request"),
     Description("Evaluate a pull request against MergeButler rules and return the grading result. " +
                 "This does NOT approve the PR — use approve_pull_request for that.")]
    public static async Task<string> GradePullRequest(
        [Description("Full URL of the pull request (e.g. https://github.com/owner/repo/pull/42)")] string prUrl,
        [Description("Platform hosting the PR: GitHub, AzureDevOps, or azdo. If not specified, inferred from git remotes.")] string? platform = null,
        [Description("Path to MergeButler YAML config file. When omitted, the effective config is built from the default user and repo locations.")] string? configPath = null,
        [Description("Auth token for the platform API. If not provided, falls back to GITHUB_TOKEN or AZURE_DEVOPS_TOKEN environment variable.")] string? token = null,
        CancellationToken cancellationToken = default)
    {
        Platform platformEnum;
        if (platform is not null)
        {
            platformEnum = PlatformServiceFactory.ParsePlatform(platform);
        }
        else
        {
            Platform? detected = PlatformServiceFactory.DetectPlatformFromGitRemotes();
            if (detected is null)
            {
                return "ERROR: Could not detect platform from git remotes. Please specify the platform parameter (GitHub, AzureDevOps, or azdo).";
            }
            platformEnum = detected.Value;
        }

        token = PlatformServiceFactory.ResolveToken(platformEnum, token);
        if (string.IsNullOrWhiteSpace(token))
        {
            string envVar = platformEnum == Platform.GitHub ? "GITHUB_TOKEN" : "AZURE_DEVOPS_TOKEN";
            return $"ERROR: No authentication token provided. Please provide a token parameter or set the {envVar} environment variable.";
        }

        // Load config
        MergeButlerConfig config;
        if (configPath is not null)
        {
            ConfigLoader loader = new();
            config = loader.Load(configPath);
        }
        else
        {
            TieredConfigManager manager = new();
            config = manager.LoadEffectiveConfig();
        }

        // Fetch PR info
        IPullRequestService service = PlatformServiceFactory.CreateService(platformEnum, token);
        PullRequestInfo prInfo = await service.GetPullRequestAsync(prUrl, cancellationToken);

        // Build rules — only deterministic rules (file globs) for MCP mode.
        // Agentic rules are reported as context for the calling LLM to evaluate.
        List<IRule> rules = [];
        List<RuleConfig> agenticRules = [];

        foreach (RuleConfig ruleConfig in config.Rules)
        {
            if (ruleConfig.Type == RuleType.FileGlob)
            {
                rules.Add(new FileGlobRule(ruleConfig));
            }
            else if (ruleConfig.Type == RuleType.Agentic)
            {
                agenticRules.Add(ruleConfig);
            }
        }

        // Run evaluation
        ExclusionEvaluator exclusionEvaluator = new();
        RuleEngine engine = new(exclusionEvaluator, rules);
        EvaluationResult result = await engine.EvaluateAsync(prInfo, config.Exclusions, cancellationToken);

        // Build detailed response
        StringBuilder sb = new();
        sb.AppendLine($"## PR Evaluation: {prInfo.Title}");
        sb.AppendLine();
        sb.AppendLine($"**Platform:** {platformEnum}");
        sb.AppendLine($"**Changed files ({prInfo.ChangedFiles.Count}):**");
        foreach (string file in prInfo.ChangedFiles)
        {
            sb.AppendLine($"  - {file}");
        }
        sb.AppendLine();

        if (result.Excluded)
        {
            sb.AppendLine($"### Result: EXCLUDED");
            sb.AppendLine($"**Reason:** {result.Reason}");
            sb.AppendLine();
            sb.AppendLine("This PR matches an exclusion pattern and should NOT be auto-approved.");
        }
        else if (result.Approved)
        {
            sb.AppendLine($"### Result: APPROVED");
            sb.AppendLine($"**Matched rule:** {result.MatchedRule}");
            sb.AppendLine($"**Reason:** {result.Reason}");
            sb.AppendLine();
            sb.AppendLine("This PR passed a deterministic rule and is safe to approve. " +
                          "Use the `approve_pull_request` tool to submit the approval.");
        }
        else
        {
            sb.AppendLine($"### Result: NOT APPROVED (by deterministic rules)");
            sb.AppendLine($"**Reason:** {result.Reason}");

            if (agenticRules.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("### Agentic Rules (require your evaluation)");
                sb.AppendLine();
                sb.AppendLine("The following rules require LLM judgment. Review the PR diff below and evaluate each:");
                sb.AppendLine();

                foreach (RuleConfig agenticRule in agenticRules)
                {
                    sb.AppendLine($"#### Rule: {agenticRule.Name}");
                    sb.AppendLine($"**Prompt:** {agenticRule.Prompt}");
                    sb.AppendLine();
                }

                sb.AppendLine("### PR Diff");
                sb.AppendLine("```");
                sb.AppendLine(prInfo.Diff);
                sb.AppendLine("```");
                sb.AppendLine();
                sb.AppendLine("If any agentic rule approves the PR, use `approve_pull_request` to submit the approval.");
            }
        }

        return sb.ToString();
    }

    [McpServerTool(Name = "approve_pull_request"),
     Description("Submit an approval on a pull request. " +
                 "Use grade_pull_request first to evaluate the PR before approving.")]
    public static async Task<string> ApprovePullRequest(
        [Description("Full URL of the pull request (e.g. https://github.com/owner/repo/pull/42)")] string prUrl,
        [Description("Platform hosting the PR: GitHub, AzureDevOps, or azdo. If not specified, inferred from git remotes.")] string? platform = null,
        [Description("Auth token for the platform API. If not provided, falls back to GITHUB_TOKEN or AZURE_DEVOPS_TOKEN environment variable.")] string? token = null,
        CancellationToken cancellationToken = default)
    {
        Platform platformEnum;
        if (platform is not null)
        {
            platformEnum = PlatformServiceFactory.ParsePlatform(platform);
        }
        else
        {
            Platform? detected = PlatformServiceFactory.DetectPlatformFromGitRemotes();
            if (detected is null)
            {
                return "ERROR: Could not detect platform from git remotes. Please specify the platform parameter (GitHub, AzureDevOps, or azdo).";
            }
            platformEnum = detected.Value;
        }

        token = PlatformServiceFactory.ResolveToken(platformEnum, token);
        if (string.IsNullOrWhiteSpace(token))
        {
            string envVar = platformEnum == Platform.GitHub ? "GITHUB_TOKEN" : "AZURE_DEVOPS_TOKEN";
            return $"ERROR: No authentication token provided. Please provide a token parameter or set the {envVar} environment variable.";
        }

        IPullRequestService service = PlatformServiceFactory.CreateService(platformEnum, token);
        await service.ApproveAsync(prUrl, cancellationToken);

        return $"Successfully approved the pull request: {prUrl}";
    }
}
