namespace MergeButler.Config;

public sealed class ExclusionConfig
{
    public string Pattern { get; set; } = string.Empty;
    public ExclusionTarget Target { get; set; } = ExclusionTarget.Both;
}

public enum ExclusionTarget
{
    Title,
    Description,
    Both
}
