using System.Text.RegularExpressions;
using MergeButler.Config;
using MergeButler.PullRequests;

namespace MergeButler.Rules;

public sealed class ExclusionEvaluator
{
    public bool IsExcluded(PullRequestInfo pullRequest, IReadOnlyList<ExclusionConfig> exclusions)
    {
        foreach (ExclusionConfig exclusion in exclusions)
        {
            if (Matches(pullRequest, exclusion))
            {
                return true;
            }
        }

        return false;
    }

    public ExclusionConfig? GetMatchingExclusion(PullRequestInfo pullRequest, IReadOnlyList<ExclusionConfig> exclusions)
    {
        foreach (ExclusionConfig exclusion in exclusions)
        {
            if (Matches(pullRequest, exclusion))
            {
                return exclusion;
            }
        }

        return null;
    }

    private static bool Matches(PullRequestInfo pullRequest, ExclusionConfig exclusion)
    {
        return exclusion.Target switch
        {
            ExclusionTarget.Title => Regex.IsMatch(pullRequest.Title, exclusion.Pattern, RegexOptions.IgnoreCase),
            ExclusionTarget.Description => Regex.IsMatch(pullRequest.Description, exclusion.Pattern, RegexOptions.IgnoreCase),
            ExclusionTarget.Both => Regex.IsMatch(pullRequest.Title, exclusion.Pattern, RegexOptions.IgnoreCase)
                                 || Regex.IsMatch(pullRequest.Description, exclusion.Pattern, RegexOptions.IgnoreCase),
            _ => false
        };
    }
}
