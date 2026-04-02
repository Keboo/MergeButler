using System.CommandLine;
using GitHub.Copilot.SDK;
using MergeButler.Config;
using MergeButler.PullRequests;
using MergeButler.Rules;

namespace MergeButler.Commands;

public static class EvaluateCommand
{
    public static Command Create()
    {
        Option<string?> configOption = new("--config", ["-c"])
        {
            Description = "Path to a MergeButler YAML configuration file. When omitted, the effective config is built from the default user (~/.mergebutler/config.yaml) and repo (.mergebutler/config.yaml) locations.",
        };

        Option<string> prOption = new("--pr")
        {
            Description = "URL or number of the pull request to evaluate. When a number is provided, the repository is inferred from the git remote.",
            Required = true
        };

        Option<Platform> platformOption = new("--platform", ["-p"])
        {
            Description = "The platform hosting the pull request.",
            Required = true
        };

        Option<string?> tokenOption = new("--token", ["-t"])
        {
            Description = "Authentication token for the platform API. Defaults to GITHUB_TOKEN or AZURE_DEVOPS_TOKEN environment variable."
        };

        Option<bool> dryRunOption = new("--dry-run", ["-n"])
        {
            Description = "Evaluate the pull request without submitting an approval."
        };

        Command command = new("evaluate", "Evaluate a pull request against configured rules and optionally approve it.")
        {
            configOption,
            prOption,
            platformOption,
            tokenOption,
            dryRunOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string? configPath = parseResult.CommandResult.GetValue(configOption);
            string prUrl = parseResult.CommandResult.GetValue(prOption)!;
            Platform platform = parseResult.CommandResult.GetValue(platformOption);
            string? token = parseResult.CommandResult.GetValue(tokenOption);
            bool dryRun = parseResult.CommandResult.GetValue(dryRunOption);

            await ExecuteAsync(configPath, prUrl, platform, token, dryRun, parseResult.InvocationConfiguration.Output, cancellationToken);
        });

        return command;
    }

    internal static async Task ExecuteAsync(
        string? configPath,
        string prUrl,
        Platform platform,
        string? token,
        bool dryRun,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        // Load configuration
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

        if (config.Exclusions.Count == 0 && config.Rules.Count == 0)
        {
            output.WriteLine("Warning: Configuration is empty. No exclusions or rules are defined.");
        }

        output.WriteLine($"Loaded configuration with {config.Exclusions.Count} exclusion(s) and {config.Rules.Count} rule(s).");

        // Resolve token from environment if not provided
        token = PlatformServiceFactory.ResolveToken(platform, token);

        if (string.IsNullOrWhiteSpace(token))
        {
            output.WriteLine("Error: No authentication token provided. Use --token or set the appropriate environment variable.");
            return;
        }

        // Create platform services
        IPullRequestService service = PlatformServiceFactory.CreateService(platform, token);

        // Resolve PR reference (URL or number) to full URL
        prUrl = await PullRequestUrlResolver.ResolveFromGitRemoteAsync(prUrl, platform);

        // Fetch PR info
        output.WriteLine($"Fetching PR info from {platform}...");
        PullRequestInfo prInfo = await service.GetPullRequestAsync(prUrl, cancellationToken);
        output.WriteLine($"PR: {prInfo.Title}");
        output.WriteLine($"Changed files: {prInfo.ChangedFiles.Count}");

        // Build rules — start Copilot client only if agentic rules exist
        bool hasAgenticRules = config.Rules.Any(r => r.Type == RuleType.Agentic);
        CopilotClient? copilotClient = null;
        IPromptEvaluator? promptEvaluator = null;

        try
        {
            if (hasAgenticRules)
            {
                output.WriteLine("Starting GitHub Copilot for agentic evaluation...");
                copilotClient = new CopilotClient(new CopilotClientOptions
                {
                    GitHubToken = token
                });
                await copilotClient.StartAsync(cancellationToken);
                promptEvaluator = new CopilotPromptEvaluator(copilotClient);
            }

            List<IRule> rules = [];
            foreach (RuleConfig ruleConfig in config.Rules)
            {
                IRule rule = ruleConfig.Type switch
                {
                    RuleType.FileGlob => new FileGlobRule(ruleConfig),
                    RuleType.Agentic => new AgenticRule(ruleConfig, promptEvaluator!),
                    _ => throw new InvalidOperationException($"Unknown rule type: {ruleConfig.Type}")
                };
                rules.Add(rule);
            }

            // Evaluate
            ExclusionEvaluator exclusionEvaluator = new();
            RuleEngine engine = new(exclusionEvaluator, rules);
            EvaluationResult result = await engine.EvaluateAsync(prInfo, config.Exclusions, cancellationToken);

            if (result.Excluded)
            {
                output.WriteLine($"EXCLUDED: {result.Reason}");
                return;
            }

            if (result.Approved)
            {
                output.WriteLine($"APPROVED by rule '{result.MatchedRule}': {result.Reason}");

                if (dryRun)
                {
                    output.WriteLine("Dry run: skipping approval submission.");
                }
                else
                {
                    await service.ApproveAsync(prUrl, cancellationToken);
                    output.WriteLine("Approval submitted successfully.");
                }
            }
            else
            {
                output.WriteLine($"NOT APPROVED: {result.Reason}");
            }
        }
        finally
        {
            if (copilotClient is not null)
            {
                await copilotClient.StopAsync();
                await copilotClient.DisposeAsync();
            }
        }
    }
}
