namespace MergeButler.PullRequests;

public interface IPullRequestProvider
{
    Task<PullRequestInfo> GetPullRequestAsync(string pullRequestUrl, CancellationToken cancellationToken = default);
}
