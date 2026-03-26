namespace MergeButler.Config;

public sealed class MergeButlerConfig
{
    public List<ExclusionConfig> Exclusions { get; set; } = [];
    public List<RuleConfig> Rules { get; set; } = [];
}
