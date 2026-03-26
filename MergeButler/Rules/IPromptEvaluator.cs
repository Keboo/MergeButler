namespace MergeButler.Rules;

/// <summary>
/// Abstraction for sending a prompt and getting a text response.
/// </summary>
public interface IPromptEvaluator
{
    Task<string> EvaluatePromptAsync(string prompt, CancellationToken cancellationToken = default);
}
