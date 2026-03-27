namespace MergeButler.PullRequests;

public interface IPullRequestService
{
    Task<PullRequestInfo> GetPullRequestAsync(string pullRequestUrl, CancellationToken cancellationToken = default);

    Task ApproveAsync(string pullRequestUrl, CancellationToken cancellationToken = default);
}
