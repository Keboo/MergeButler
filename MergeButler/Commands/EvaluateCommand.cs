using System.CommandLine;
using GitHub.Copilot.SDK;
using MergeButler.Config;
using MergeButler.PullRequests;
using MergeButler.Rules;
using Octokit;

namespace MergeButler.Commands;

public static class EvaluateCommand
{
    public static Command Create()
    {
        Option<string> configOption = new("--config", ["-c"])
        {
            Description = "Path to the MergeButler YAML configuration file.",
            DefaultValueFactory = _ => "mergebutler.yaml",
            Required = true
        };

        Option<string> prOption = new("--pr")
        {
            Description = "URL of the pull request to evaluate.",
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

        Command command = new("evaluate", "Evaluate a pull request against configured rules and optionally approve it.")
        {
            configOption,
            prOption,
            platformOption,
            tokenOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string configPath = parseResult.CommandResult.GetValue(configOption)!;
            string prUrl = parseResult.CommandResult.GetValue(prOption)!;
            Platform platform = parseResult.CommandResult.GetValue(platformOption);
            string? token = parseResult.CommandResult.GetValue(tokenOption);

            await ExecuteAsync(configPath, prUrl, platform, token, parseResult.InvocationConfiguration.Output, cancellationToken);
        });

        return command;
    }

    internal static async Task ExecuteAsync(
        string configPath,
        string prUrl,
        Platform platform,
        string? token,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        // Load configuration
        ConfigLoader loader = new();
        MergeButlerConfig config = loader.Load(configPath);
        output.WriteLine($"Loaded configuration with {config.Exclusions.Count} exclusion(s) and {config.Rules.Count} rule(s).");

        // Resolve token from environment if not provided
        token ??= platform switch
        {
            Platform.GitHub => Environment.GetEnvironmentVariable("GITHUB_TOKEN"),
            Platform.AzureDevOps => Environment.GetEnvironmentVariable("AZURE_DEVOPS_TOKEN"),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(token))
        {
            output.WriteLine("Error: No authentication token provided. Use --token or set the appropriate environment variable.");
            return;
        }

        // Create platform services
        IPullRequestProvider provider;
        IPullRequestApprover approver;

        switch (platform)
        {
            case Platform.GitHub:
                GitHubClient ghClient = new(new ProductHeaderValue("MergeButler"))
                {
                    Credentials = new Credentials(token)
                };
                GitHubPullRequestService ghService = new(ghClient);
                provider = ghService;
                approver = ghService;
                break;

            case Platform.AzureDevOps:
                HttpClient httpClient = new();
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                        Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{token}")));
                AzureDevOpsPullRequestService azdoService = new(httpClient);
                provider = azdoService;
                approver = azdoService;
                break;

            default:
                output.WriteLine($"Error: Unsupported platform '{platform}'.");
                return;
        }

        // Fetch PR info
        output.WriteLine($"Fetching PR info from {platform}...");
        PullRequestInfo prInfo = await provider.GetPullRequestAsync(prUrl, cancellationToken);
        output.WriteLine($"PR: {prInfo.Title}");
        output.WriteLine($"Changed files: {prInfo.ChangedFiles.Count}");

        // Build rules — start Copilot client only if agentic rules exist
        bool hasAgenticRules = config.Rules.Any(r => r.Type == RuleType.Agentic);
        GitHub.Copilot.SDK.CopilotClient? copilotClient = null;
        IPromptEvaluator? promptEvaluator = null;

        try
        {
            if (hasAgenticRules)
            {
                output.WriteLine("Starting GitHub Copilot for agentic evaluation...");
                copilotClient = new GitHub.Copilot.SDK.CopilotClient(new CopilotClientOptions
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
                await approver.ApproveAsync(prUrl, cancellationToken);
                output.WriteLine("Approval submitted successfully.");
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
