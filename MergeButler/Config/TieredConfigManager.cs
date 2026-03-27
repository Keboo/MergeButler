using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MergeButler.Config;

public enum ConfigScope
{
    User,
    Repo
}

public sealed record SourcedExclusion(ExclusionConfig Exclusion, ConfigScope Source);
public sealed record SourcedRule(RuleConfig Rule, ConfigScope Source);

public sealed class EffectiveConfig
{
    public List<SourcedExclusion> Exclusions { get; } = [];
    public List<SourcedRule> Rules { get; } = [];
}

public sealed class TieredConfigManager
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
        .Build();

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public string UserConfigPath { get; }
    public string RepoConfigPath { get; }

    public TieredConfigManager()
        : this(GetDefaultUserConfigPath(), GetDefaultRepoConfigPath())
    {
    }

    public TieredConfigManager(string userConfigPath, string repoConfigPath)
    {
        UserConfigPath = userConfigPath;
        RepoConfigPath = repoConfigPath;
    }

    /// <summary>
    /// Returns the effective config by merging user and repo levels.
    /// Repo-level items take precedence (exclusions by pattern, rules by name).
    /// </summary>
    public EffectiveConfig GetEffectiveConfig()
    {
        MergeButlerConfig userConfig = LoadConfig(UserConfigPath);
        MergeButlerConfig repoConfig = LoadConfig(RepoConfigPath);

        EffectiveConfig effective = new();

        // Merge exclusions: repo overrides user by pattern
        HashSet<string> repoPatterns = new(
            repoConfig.Exclusions.Select(e => e.Pattern),
            StringComparer.Ordinal);

        foreach (ExclusionConfig exclusion in userConfig.Exclusions)
        {
            if (!repoPatterns.Contains(exclusion.Pattern))
            {
                effective.Exclusions.Add(new SourcedExclusion(exclusion, ConfigScope.User));
            }
        }

        foreach (ExclusionConfig exclusion in repoConfig.Exclusions)
        {
            effective.Exclusions.Add(new SourcedExclusion(exclusion, ConfigScope.Repo));
        }

        // Merge rules: repo overrides user by name
        HashSet<string> repoRuleNames = new(
            repoConfig.Rules.Select(r => r.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (RuleConfig rule in userConfig.Rules)
        {
            if (!repoRuleNames.Contains(rule.Name))
            {
                effective.Rules.Add(new SourcedRule(rule, ConfigScope.User));
            }
        }

        foreach (RuleConfig rule in repoConfig.Rules)
        {
            effective.Rules.Add(new SourcedRule(rule, ConfigScope.Repo));
        }

        return effective;
    }

    /// <summary>
    /// Returns a flat MergeButlerConfig with merged exclusions and rules (for use by the rule engine).
    /// </summary>
    public MergeButlerConfig LoadEffectiveConfig()
    {
        EffectiveConfig effective = GetEffectiveConfig();
        return new MergeButlerConfig
        {
            Exclusions = effective.Exclusions.Select(e => e.Exclusion).ToList(),
            Rules = effective.Rules.Select(r => r.Rule).ToList()
        };
    }

    public void SetExclusion(string pattern, ExclusionTarget target, ConfigScope scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        string path = scope == ConfigScope.User ? UserConfigPath : RepoConfigPath;
        MergeButlerConfig config = LoadConfig(path);

        // Update existing or add new
        ExclusionConfig? existing = config.Exclusions.FirstOrDefault(
            e => string.Equals(e.Pattern, pattern, StringComparison.Ordinal));

        if (existing is not null)
        {
            existing.Target = target;
        }
        else
        {
            config.Exclusions.Add(new ExclusionConfig { Pattern = pattern, Target = target });
        }

        SaveConfig(path, config);
    }

    public void SetRule(RuleConfig rule, ConfigScope scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rule.Name);

        string path = scope == ConfigScope.User ? UserConfigPath : RepoConfigPath;
        MergeButlerConfig config = LoadConfig(path);

        // Replace existing or add new
        int index = config.Rules.FindIndex(
            r => string.Equals(r.Name, rule.Name, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            config.Rules[index] = rule;
        }
        else
        {
            config.Rules.Add(rule);
        }

        SaveConfig(path, config);
    }

    private static MergeButlerConfig LoadConfig(string path)
    {
        if (!File.Exists(path))
        {
            return new MergeButlerConfig();
        }

        string yaml = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return new MergeButlerConfig();
        }

        return Deserializer.Deserialize<MergeButlerConfig>(yaml) ?? new MergeButlerConfig();
    }

    private static void SaveConfig(string path, MergeButlerConfig config)
    {
        string? directory = Path.GetDirectoryName(path);
        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string yaml = Serializer.Serialize(config);
        File.WriteAllText(path, yaml);
    }

    private static string GetDefaultUserConfigPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".mergebutler",
            "config.yaml");

    private static string GetDefaultRepoConfigPath() =>
        Path.Combine(FindRepoRoot() ?? Directory.GetCurrentDirectory(), ".mergebutler", "config.yaml");

    private static string? FindRepoRoot()
    {
        DirectoryInfo? dir = new(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
