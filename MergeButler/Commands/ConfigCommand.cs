using System.CommandLine;
using MergeButler.Config;

namespace MergeButler.Commands;

public static class ConfigCommand
{
    public static Command Create()
    {
        Command command = new("config", "View or modify MergeButler configuration (exclusions and rules).")
        {
            CreateShowCommand(),
            CreateSetExclusionCommand(),
            CreateSetRuleCommand()
        };

        return command;
    }

    private static Command CreateShowCommand()
    {
        Command command = new("show", "Display the effective merged configuration and where each item comes from.");

        command.SetAction((parseResult, _) =>
        {
            TextWriter output = parseResult.InvocationConfiguration.Output;
            TieredConfigManager manager = new();
            EffectiveConfig effective = manager.GetEffectiveConfig();

            output.WriteLine("Exclusions:");
            if (effective.Exclusions.Count == 0)
            {
                output.WriteLine("  (none)");
            }

            foreach (SourcedExclusion entry in effective.Exclusions)
            {
                string scope = entry.Source.ToString().ToLowerInvariant();
                output.WriteLine($"  [{scope}] \"{entry.Exclusion.Pattern}\" (target: {entry.Exclusion.Target.ToString().ToLowerInvariant()})");
            }

            output.WriteLine();
            output.WriteLine("Rules:");
            if (effective.Rules.Count == 0)
            {
                output.WriteLine("  (none)");
            }

            foreach (SourcedRule entry in effective.Rules)
            {
                string scope = entry.Source.ToString().ToLowerInvariant();
                string detail = entry.Rule.Type switch
                {
                    RuleType.FileGlob => $"fileGlob: {string.Join(", ", entry.Rule.Patterns)}",
                    RuleType.Agentic => $"agentic: {Truncate(entry.Rule.Prompt, 60)}",
                    _ => entry.Rule.Type.ToString()
                };
                output.WriteLine($"  [{scope}] \"{entry.Rule.Name}\" ({detail})");
            }

            output.WriteLine();
            output.WriteLine($"User config:  {manager.UserConfigPath}");
            output.WriteLine($"Repo config:  {manager.RepoConfigPath}");

            return Task.CompletedTask;
        });

        return command;
    }

    private static Command CreateSetExclusionCommand()
    {
        Argument<string> patternArgument = new("pattern")
        {
            Description = "The exclusion pattern to add or update."
        };

        Option<ExclusionTarget> targetOption = new("--target")
        {
            Description = "What the pattern matches against.",
            DefaultValueFactory = _ => ExclusionTarget.Both
        };

        Option<ConfigScope> scopeOption = new("--scope", ["-s"])
        {
            Description = "Where to save: user-level or repo-level.",
            DefaultValueFactory = _ => ConfigScope.Repo
        };

        Command command = new("set-exclusion", "Add or update an exclusion pattern.")
        {
            patternArgument,
            targetOption,
            scopeOption
        };

        command.SetAction((parseResult, _) =>
        {
            TextWriter output = parseResult.InvocationConfiguration.Output;
            string pattern = parseResult.CommandResult.GetValue(patternArgument)!;
            ExclusionTarget target = parseResult.CommandResult.GetValue(targetOption);
            ConfigScope scope = parseResult.CommandResult.GetValue(scopeOption);

            TieredConfigManager manager = new();
            manager.SetExclusion(pattern, target, scope);

            output.WriteLine($"Set exclusion \"{pattern}\" (target: {target.ToString().ToLowerInvariant()}) at {scope.ToString().ToLowerInvariant()} level.");

            return Task.CompletedTask;
        });

        return command;
    }

    private static Command CreateSetRuleCommand()
    {
        Argument<string> nameArgument = new("name")
        {
            Description = "The rule name (used as unique identifier)."
        };

        Option<RuleType> typeOption = new("--type")
        {
            Description = "The rule type.",
            Required = true
        };

        Option<string[]> patternsOption = new("--patterns")
        {
            Description = "File glob patterns (for fileGlob rules). Specify multiple: --patterns *.md --patterns docs/**"
        };

        Option<string?> promptOption = new("--prompt")
        {
            Description = "Evaluation prompt (for agentic rules)."
        };

        Option<ConfigScope> scopeOption = new("--scope", ["-s"])
        {
            Description = "Where to save: user-level or repo-level.",
            DefaultValueFactory = _ => ConfigScope.Repo
        };

        Command command = new("set-rule", "Add or update a rule.")
        {
            nameArgument,
            typeOption,
            patternsOption,
            promptOption,
            scopeOption
        };

        command.SetAction((parseResult, _) =>
        {
            TextWriter output = parseResult.InvocationConfiguration.Output;
            string name = parseResult.CommandResult.GetValue(nameArgument)!;
            RuleType type = parseResult.CommandResult.GetValue(typeOption);
            string[]? patterns = parseResult.CommandResult.GetValue(patternsOption);
            string? prompt = parseResult.CommandResult.GetValue(promptOption);
            ConfigScope scope = parseResult.CommandResult.GetValue(scopeOption);

            RuleConfig rule = new()
            {
                Name = name,
                Type = type,
                Patterns = patterns?.ToList() ?? [],
                Prompt = prompt ?? string.Empty
            };

            // Validate
            switch (type)
            {
                case RuleType.FileGlob when rule.Patterns.Count == 0:
                    output.WriteLine("Error: fileGlob rules require at least one --patterns value.");
                    return Task.CompletedTask;
                case RuleType.Agentic when string.IsNullOrWhiteSpace(rule.Prompt):
                    output.WriteLine("Error: agentic rules require a --prompt value.");
                    return Task.CompletedTask;
            }

            TieredConfigManager manager = new();
            manager.SetRule(rule, scope);

            output.WriteLine($"Set rule \"{name}\" (type: {type}) at {scope.ToString().ToLowerInvariant()} level.");

            return Task.CompletedTask;
        });

        return command;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : $"{value[..maxLength]}...";
}
