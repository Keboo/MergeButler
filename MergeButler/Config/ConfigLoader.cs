using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MergeButler.Config;

public sealed class ConfigLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public MergeButlerConfig Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {filePath}", filePath);
        }

        string yaml = File.ReadAllText(filePath);
        return LoadFromYaml(yaml);
    }

    public MergeButlerConfig LoadFromYaml(string yaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaml);

        MergeButlerConfig config = Deserializer.Deserialize<MergeButlerConfig>(yaml)
            ?? throw new InvalidOperationException("Failed to deserialize configuration.");

        Validate(config);
        return config;
    }

    private static void Validate(MergeButlerConfig config)
    {
        for (int i = 0; i < config.Exclusions.Count; i++)
        {
            ExclusionConfig exclusion = config.Exclusions[i];
            if (string.IsNullOrWhiteSpace(exclusion.Pattern))
            {
                throw new InvalidOperationException($"Exclusion at index {i} has an empty pattern.");
            }
        }

        for (int i = 0; i < config.Rules.Count; i++)
        {
            RuleConfig rule = config.Rules[i];
            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                throw new InvalidOperationException($"Rule at index {i} has an empty name.");
            }

            switch (rule.Type)
            {
                case RuleType.FileGlob when rule.Patterns.Count == 0:
                    throw new InvalidOperationException($"Rule '{rule.Name}' is a FileGlob rule but has no patterns.");
                case RuleType.Agentic when string.IsNullOrWhiteSpace(rule.Prompt):
                    throw new InvalidOperationException($"Rule '{rule.Name}' is an Agentic rule but has no prompt.");
            }
        }
    }
}
