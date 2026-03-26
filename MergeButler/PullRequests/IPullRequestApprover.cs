namespace MergeButler.PullRequests;

public interface IPullRequestApprover
{
    Task ApproveAsync(string pullRequestUrl, CancellationToken cancellationToken = default);
}
