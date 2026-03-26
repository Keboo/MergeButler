using GitHub.Copilot.SDK;

namespace MergeButler.Rules;

/// <summary>
/// Evaluates prompts using the GitHub Copilot SDK.
/// The caller is responsible for managing the <see cref="CopilotClient"/> lifecycle.
/// </summary>
public sealed class CopilotPromptEvaluator : IPromptEvaluator
{
    private readonly CopilotClient _client;

    public CopilotPromptEvaluator(CopilotClient client)
    {
        _client = client;
    }

    public async Task<string> EvaluatePromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        await using CopilotSession session = await _client.CreateSessionAsync(new SessionConfig
        {
            OnPermissionRequest = PermissionHandler.ApproveAll,
            SystemMessage = new SystemMessageConfig
            {
                Content = """
                    You are a pull request reviewer. You will be given a PR diff and a rule description.
                    Evaluate whether the PR should be approved based on the rule.
                    Respond with EXACTLY one of these two words on the first line: APPROVE or REJECT
                    Then on the next line, provide a brief reason.
                    Do not use any tools. Only analyze the information provided.
                    """
            }
        });

        TaskCompletionSource<string> completionSource = new();
        string content = string.Empty;

        using IDisposable subscription = session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent msg:
                    content = msg.Data.Content ?? string.Empty;
                    break;
                case SessionIdleEvent:
                    completionSource.TrySetResult(content);
                    break;
                case SessionErrorEvent error:
                    completionSource.TrySetException(
                        new InvalidOperationException($"Copilot session error: {error.Data.Message}"));
                    break;
            }
        });

        await session.SendAsync(new MessageOptions { Prompt = prompt });

        // Support cancellation
        using CancellationTokenRegistration registration = cancellationToken.Register(
            () => completionSource.TrySetCanceled(cancellationToken));

        return await completionSource.Task;
    }
}
