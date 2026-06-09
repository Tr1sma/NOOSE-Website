namespace NOOSE_Website.Services;

/// <summary>
/// Schreibt Lese-/Zugriffsprotokolle: wer hat wann welche (sensible) Akte angesehen.
/// Wird ab Phase 2 aus den Detailansichten heraus aufgerufen.
/// </summary>
public interface IZugriffsLogService
{
    Task LogAnsichtAsync(string entitaetTyp, string entitaetId, CancellationToken cancellationToken = default);
}
