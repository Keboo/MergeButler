namespace MergeButler.Config;

public sealed class RuleConfig
{
    public string Name { get; set; } = string.Empty;
    public RuleType Type { get; set; }
    public List<string> Patterns { get; set; } = [];
    public string Prompt { get; set; } = string.Empty;
}

public enum RuleType
{
    FileGlob,
    Agentic
}
