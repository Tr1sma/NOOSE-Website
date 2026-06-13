namespace NOOSE_Website.Services;

/// <summary>
/// Berechnet &amp; persistiert den automatischen Bedrohungs-Score einer Fraktion (Algorithmus „EHK-Score",
/// siehe AlgoPlan.md). Schreibt <c>BedrohungsScore</c>/<c>BedrohungsKonfidenz</c>/<c>BedrohungsDetailJson</c>/
/// <c>ScoreBerechnetAm</c> via <c>ExecuteUpdateAsync</c> am Audit-Interceptor vorbei (kein <c>GeaendertAm</c>-Stempel,
/// keine AuditLog-Flut). Wird ereignisgetrieben aus den schreibenden Diensten und zeitgetrieben aus dem
/// nächtlichen Sweep-Dienst aufgerufen.
/// </summary>
public interface IBedrohungsScoreService
{
    /// <summary>Berechnet den Score einer Fraktion neu und persistiert ihn. Idempotent; eigener DbContext.
    /// Gelöschte Fraktionen werden übersprungen, Staatsfraktionen auf <c>null</c> gesetzt.</summary>
    Task NeuBerechnenAsync(string fraktionId, CancellationToken cancellationToken = default);

    /// <summary>Rechnet alle Fraktionen neu, in denen die Person je Mitglied war (austritts-stabil), neu –
    /// nach einer Änderung an deren Maßnahmen-Doks aufzurufen.</summary>
    Task NeuBerechnenFuerPersonAsync(string personId, CancellationToken cancellationToken = default);

    /// <summary>Rechnet alle nicht gelöschten Fraktionen neu (nächtlicher Sweep gegen Decay-Drift).
    /// Liefert die Anzahl tatsächlich berechneter Fraktionen.</summary>
    Task<int> NeuBerechnenAlleAsync(CancellationToken cancellationToken = default);

    /// <summary>Berechnet den Score einer Person neu und persistiert ihn. Idempotent; eigener DbContext.</summary>
    Task NeuBerechnenPersonScoreAsync(string personId, CancellationToken cancellationToken = default);

    /// <summary>Rechnet alle nicht gelöschten Personen neu (Sweep gegen Decay-Drift). Liefert die Anzahl.</summary>
    Task<int> NeuBerechnenAllePersonenScoresAsync(CancellationToken cancellationToken = default);
}
