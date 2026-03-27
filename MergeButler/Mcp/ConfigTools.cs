using System.ComponentModel;
using System.Text;
using MergeButler.Config;
using ModelContextProtocol.Server;

namespace MergeButler.Mcp;

[McpServerToolType]
public class ConfigTools
{
    [McpServerTool(Name = "get_config"),
     Description("Get the effective MergeButler configuration. " +
                 "Shows all exclusions and rules merged from user-level (~/.mergebutler/config.yaml) " +
                 "and repo-level (.mergebutler/config.yaml), with the source of each item.")]
    public static string GetConfig()
    {
        TieredConfigManager manager = new();
        EffectiveConfig effective = manager.GetEffectiveConfig();

        StringBuilder sb = new();
        sb.AppendLine("## MergeButler Configuration");
        sb.AppendLine();

        sb.AppendLine("### Exclusions");
        if (effective.Exclusions.Count == 0)
        {
            sb.AppendLine("*(none)*");
        }

        foreach (SourcedExclusion entry in effective.Exclusions)
        {
            sb.AppendLine($"- `\"{entry.Exclusion.Pattern}\"` target: {entry.Exclusion.Target.ToString().ToLowerInvariant()} *(source: {entry.Source.ToString().ToLowerInvariant()})*");
        }

        sb.AppendLine();
        sb.AppendLine("### Rules");
        if (effective.Rules.Count == 0)
        {
            sb.AppendLine("*(none)*");
        }

        foreach (SourcedRule entry in effective.Rules)
        {
            string detail = entry.Rule.Type switch
            {
                RuleType.FileGlob => $"fileGlob — patterns: {string.Join(", ", entry.Rule.Patterns)}",
                RuleType.Agentic => $"agentic — prompt: {entry.Rule.Prompt}",
                _ => entry.Rule.Type.ToString()
            };
            sb.AppendLine($"- **{entry.Rule.Name}**: {detail} *(source: {entry.Source.ToString().ToLowerInvariant()})*");
        }

        sb.AppendLine();
        sb.AppendLine($"User config path: `{manager.UserConfigPath}`");
        sb.AppendLine($"Repo config path: `{manager.RepoConfigPath}`");

        return sb.ToString();
    }

    [McpServerTool(Name = "set_exclusion"),
     Description("Add or update an exclusion pattern in MergeButler configuration. " +
                 "Exclusions prevent PRs from being auto-approved when the pattern matches.")]
    public static string SetExclusion(
        [Description("The exclusion pattern (matched against PR title/description)")] string pattern,
        [Description("What to match against: Title, Description, or Both")] string target = "Both",
        [Description("Where to save: 'user' for user-level or 'repo' for repo-level")] string scope = "repo")
    {
        if (!Enum.TryParse<ConfigScope>(scope, ignoreCase: true, out ConfigScope configScope))
        {
            return $"ERROR: Invalid scope '{scope}'. Must be 'user' or 'repo'.";
        }

        if (!Enum.TryParse<ExclusionTarget>(target, ignoreCase: true, out ExclusionTarget exclusionTarget))
        {
            return $"ERROR: Invalid target '{target}'. Must be 'Title', 'Description', or 'Both'.";
        }

        TieredConfigManager manager = new();
        manager.SetExclusion(pattern, exclusionTarget, configScope);
        return $"Set exclusion \"{pattern}\" (target: {exclusionTarget.ToString().ToLowerInvariant()}) at {configScope.ToString().ToLowerInvariant()} level.";
    }

    [McpServerTool(Name = "set_rule"),
     Description("Add or update a rule in MergeButler configuration. " +
                 "Rules define conditions under which PRs can be auto-approved. " +
                 "FileGlob rules require patterns; Agentic rules require a prompt.")]
    public static string SetRule(
        [Description("The rule name (unique identifier)")] string name,
        [Description("The rule type: FileGlob or Agentic")] string type,
        [Description("File glob patterns for FileGlob rules (comma-separated, e.g. '**/*.md,docs/**')")] string? patterns = null,
        [Description("Evaluation prompt for Agentic rules")] string? prompt = null,
        [Description("Where to save: 'user' for user-level or 'repo' for repo-level")] string scope = "repo")
    {
        if (!Enum.TryParse<ConfigScope>(scope, ignoreCase: true, out ConfigScope configScope))
        {
            return $"ERROR: Invalid scope '{scope}'. Must be 'user' or 'repo'.";
        }

        if (!Enum.TryParse<RuleType>(type, ignoreCase: true, out RuleType ruleType))
        {
            return $"ERROR: Invalid rule type '{type}'. Must be 'FileGlob' or 'Agentic'.";
        }

        RuleConfig rule = new()
        {
            Name = name,
            Type = ruleType,
            Patterns = patterns?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? [],
            Prompt = prompt ?? string.Empty
        };

        switch (ruleType)
        {
            case RuleType.FileGlob when rule.Patterns.Count == 0:
                return "ERROR: FileGlob rules require at least one pattern (use the patterns parameter).";
            case RuleType.Agentic when string.IsNullOrWhiteSpace(rule.Prompt):
                return "ERROR: Agentic rules require a prompt.";
        }

        TieredConfigManager manager = new();
        manager.SetRule(rule, configScope);
        return $"Set rule \"{name}\" (type: {ruleType}) at {configScope.ToString().ToLowerInvariant()} level.";
    }
}
