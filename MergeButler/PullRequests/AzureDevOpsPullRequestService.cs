using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MergeButler.PullRequests;

public sealed partial class AzureDevOpsPullRequestService : IPullRequestProvider, IPullRequestApprover
{
    private readonly HttpClient _httpClient;

    public AzureDevOpsPullRequestService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PullRequestInfo> GetPullRequestAsync(string pullRequestUrl, CancellationToken cancellationToken = default)
    {
        (string organization, string project, string repository, int pullRequestId) = ParsePullRequestUrl(pullRequestUrl);

        string baseUrl = $"https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repository}/pullRequests/{pullRequestId}";

        // Get PR details
        AzDoGetPullRequestResponse? pr = await _httpClient.GetFromJsonAsync<AzDoGetPullRequestResponse>(
            $"{baseUrl}?api-version=7.1", cancellationToken)
            ?? throw new InvalidOperationException("Failed to get PR details from Azure DevOps.");

        // Get iterations to build diff
        AzDoListResponse<AzDoIteration>? iterations = await _httpClient.GetFromJsonAsync<AzDoListResponse<AzDoIteration>>(
            $"{baseUrl}/iterations?api-version=7.1", cancellationToken);

        List<string> changedFiles = [];
        string diff = string.Empty;

        if (iterations?.Value.Count > 0)
        {
            int lastIteration = iterations.Value.Count;

            // Get changes for the latest iteration
            AzDoListResponse<AzDoChange>? changes = await _httpClient.GetFromJsonAsync<AzDoListResponse<AzDoChange>>(
                $"{baseUrl}/iterations/{lastIteration}/changes?api-version=7.1", cancellationToken);

            if (changes?.Value is not null)
            {
                changedFiles = changes.Value
                    .Where(c => c.Item?.Path is not null)
                    .Select(c => c.Item!.Path!.TrimStart('/'))
                    .ToList();
            }
        }

        return new PullRequestInfo
        {
            Title = pr.Title ?? string.Empty,
            Description = pr.Description ?? string.Empty,
            ChangedFiles = changedFiles,
            Diff = diff
        };
    }

    public async Task ApproveAsync(string pullRequestUrl, CancellationToken cancellationToken = default)
    {
        (string organization, string project, string repository, int pullRequestId) = ParsePullRequestUrl(pullRequestUrl);

        // Get the current user's identity
        AzDoConnectionData? connectionData = await _httpClient.GetFromJsonAsync<AzDoConnectionData>(
            $"https://dev.azure.com/{organization}/_apis/connectionData?api-version=7.1", cancellationToken)
            ?? throw new InvalidOperationException("Failed to get connection data from Azure DevOps.");

        string reviewerUrl = $"https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repository}/pullRequests/{pullRequestId}/reviewers/{connectionData.AuthenticatedUser?.Id}?api-version=7.1";

        var reviewBody = new { vote = 10 }; // 10 = Approved
        using HttpRequestMessage request = new(HttpMethod.Put, reviewerUrl)
        {
            Content = JsonContent.Create(reviewBody)
        };

        HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public static (string Organization, string Project, string Repository, int PullRequestId) ParsePullRequestUrl(string url)
    {
        // Supports: https://dev.azure.com/{org}/{project}/_git/{repo}/pullrequest/{id}
        Match match = AzDoPrUrlPattern().Match(url);
        if (!match.Success)
        {
            throw new ArgumentException($"Invalid Azure DevOps PR URL: {url}", nameof(url));
        }

        return (
            match.Groups["org"].Value,
            match.Groups["project"].Value,
            match.Groups["repo"].Value,
            int.Parse(match.Groups["id"].Value));
    }

    [GeneratedRegex(@"dev\.azure\.com/(?<org>[^/]+)/(?<project>[^/]+)/_git/(?<repo>[^/]+)/pullrequest/(?<id>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex AzDoPrUrlPattern();

    // Internal DTOs for Azure DevOps REST API responses
    private sealed class AzDoGetPullRequestResponse
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    private sealed class AzDoListResponse<T>
    {
        [JsonPropertyName("value")]
        public List<T> Value { get; set; } = [];

        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    private sealed class AzDoIteration
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    private sealed class AzDoChange
    {
        [JsonPropertyName("item")]
        public AzDoChangeItem? Item { get; set; }
    }

    private sealed class AzDoChangeItem
    {
        [JsonPropertyName("path")]
        public string? Path { get; set; }
    }

    private sealed class AzDoConnectionData
    {
        [JsonPropertyName("authenticatedUser")]
        public AzDoIdentity? AuthenticatedUser { get; set; }
    }

    private sealed class AzDoIdentity
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
