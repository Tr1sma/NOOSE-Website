namespace NOOSE_Website.Infrastructure.Shares;

/// <summary>Process-wide in-memory broadcaster for live updates of a document's access list.</summary>
public sealed class DocumentAccessBroadcaster
{
    /// <summary>Fired with the document id whose access list may have changed.</summary>
    public event Action<string>? Modified;

    public void Report(string documentId) => Modified?.Invoke(documentId);
}
