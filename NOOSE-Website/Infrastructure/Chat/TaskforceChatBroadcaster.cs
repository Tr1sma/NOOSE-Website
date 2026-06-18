namespace NOOSE_Website.Infrastructure.Chat;

/// <summary>Process-wide in-memory broadcaster for live taskforce-chat updates.</summary>
public sealed class TaskforceChatBroadcaster
{
    /// <summary>Fired with the affected taskforce id when its chat changes.</summary>
    public event Action<string>? Modified;

    public void Report(string taskforceId) => Modified?.Invoke(taskforceId);
}
