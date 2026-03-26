using MergeButler.Config;

namespace MergeButler.Tests.Config;

public class ConfigLoaderTests
{
    private readonly ConfigLoader _loader = new();

    [Fact]
    public void LoadFromYaml_ValidConfig_DeserializesCorrectly()
    {
        string yaml = """
            exclusions:
              - pattern: "DO NOT AUTO-APPROVE"
                target: title
              - pattern: "\\[manual review\\]"
                target: both
            rules:
              - name: "Documentation only"
                type: fileGlob
                patterns:
                  - "**/*.md"
                  - "docs/**"
              - name: "Safe updates"
                type: agentic
                prompt: "Approve if only package versions changed."
            """;

        MergeButlerConfig config = _loader.LoadFromYaml(yaml);

        Assert.Equal(2, config.Exclusions.Count);
        Assert.Equal("DO NOT AUTO-APPROVE", config.Exclusions[0].Pattern);
        Assert.Equal(ExclusionTarget.Title, config.Exclusions[0].Target);
        Assert.Equal(ExclusionTarget.Both, config.Exclusions[1].Target);

        Assert.Equal(2, config.Rules.Count);
        Assert.Equal("Documentation only", config.Rules[0].Name);
        Assert.Equal(RuleType.FileGlob, config.Rules[0].Type);
        Assert.Equal(2, config.Rules[0].Patterns.Count);
        Assert.Equal("Safe updates", config.Rules[1].Name);
        Assert.Equal(RuleType.Agentic, config.Rules[1].Type);
        Assert.Equal("Approve if only package versions changed.", config.Rules[1].Prompt);
    }

    [Fact]
    public void LoadFromYaml_EmptyExclusions_DefaultsToEmptyList()
    {
        string yaml = """
            rules:
              - name: "Docs"
                type: fileGlob
                patterns:
                  - "**/*.md"
            """;

        MergeButlerConfig config = _loader.LoadFromYaml(yaml);

        Assert.Empty(config.Exclusions);
        Assert.Single(config.Rules);
    }

    [Fact]
    public void LoadFromYaml_EmptyRules_DefaultsToEmptyList()
    {
        string yaml = """
            exclusions:
              - pattern: "skip"
                target: title
            """;

        MergeButlerConfig config = _loader.LoadFromYaml(yaml);

        Assert.Single(config.Exclusions);
        Assert.Empty(config.Rules);
    }

    [Fact]
    public void LoadFromYaml_FileGlobRuleWithNoPatterns_ThrowsValidation()
    {
        string yaml = """
            rules:
              - name: "Bad rule"
                type: fileGlob
                patterns: []
            """;

        Assert.Throws<InvalidOperationException>(() => _loader.LoadFromYaml(yaml));
    }

    [Fact]
    public void LoadFromYaml_AgenticRuleWithNoPrompt_ThrowsValidation()
    {
        string yaml = """
            rules:
              - name: "Bad agentic"
                type: agentic
            """;

        Assert.Throws<InvalidOperationException>(() => _loader.LoadFromYaml(yaml));
    }

    [Fact]
    public void LoadFromYaml_ExclusionWithEmptyPattern_ThrowsValidation()
    {
        string yaml = """
            exclusions:
              - pattern: ""
                target: title
            """;

        Assert.Throws<InvalidOperationException>(() => _loader.LoadFromYaml(yaml));
    }

    [Fact]
    public void LoadFromYaml_EmptyYaml_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _loader.LoadFromYaml(""));
        Assert.Throws<ArgumentException>(() => _loader.LoadFromYaml("   "));
    }

    [Fact]
    public void Load_FileNotFound_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => _loader.Load("nonexistent.yml"));
    }
}
