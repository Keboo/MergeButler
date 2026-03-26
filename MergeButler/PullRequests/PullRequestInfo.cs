namespace MergeButler.PullRequests;

public sealed class PullRequestInfo
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<string> ChangedFiles { get; init; }
    public required string Diff { get; init; }
}
