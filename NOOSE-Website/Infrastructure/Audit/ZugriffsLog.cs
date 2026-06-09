namespace NOOSE_Website.Infrastructure.Audit;

/// <summary>
/// Lese-/Zugriffsprotokoll: hält fest, wer wann eine (sensible) Akte angesehen hat.
/// Wird über den <c>IZugriffsLogService</c> explizit aus den Detailansichten geschrieben.
/// </summary>
public class ZugriffsLog
{
    public long Id { get; set; }
    public DateTime Zeitpunkt { get; set; }
    public string? AgentId { get; set; }
    public string? AgentName { get; set; }
    public string EntitaetTyp { get; set; } = string.Empty;
    public string EntitaetId { get; set; } = string.Empty;
}
