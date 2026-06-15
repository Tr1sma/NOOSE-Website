using System.Security.Claims;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Authorization;

/// <summary>
/// Bündelt die Sichtbarkeits-Berechtigungen eines Betrachters für die Verschlusssache-Stufen der
/// Dokumenten-/Datei-Bibliothek. Wird an die Dienste übergeben, damit deren EF-Abfragen die Stufen
/// (<see cref="DocumentClassification"/>) serverseitig filtern können, ohne pro Anfrage den
/// <see cref="ClaimsPrincipal"/> auswerten zu müssen.
/// </summary>
/// <param name="MayClassified">Darf Führungs-Verschlusssachen lesen (Führung/Admin oder Nur-Lese-Aufsicht) –
/// und damit auch alle übrigen Stufen (die Führung sieht jede Verschlusssache).</param>
/// <param name="IsTru">Gehört der Tactical Response Unit an (sieht TRU-Verschlusssachen).</param>
/// <param name="IsHrb">Gehört dem Human Resources Branch an (sieht HRB-Verschlusssachen).</param>
public readonly record struct DocumentViewerScope(bool MayClassified, bool IsTru, bool IsHrb)
{
    /// <summary>Leitet den Sichtbarkeits-Umfang aus den Claims des angemeldeten Agents ab.</summary>
    public static DocumentViewerScope From(ClaimsPrincipal user)
        => new(user.MayClassifiedRead(), user.IsTRU(), user.IsHRB());

    /// <summary>True, wenn der Betrachter ein Dokument der angegebenen Stufe sehen darf.</summary>
    public bool CanSee(DocumentClassification classification) => classification switch
    {
        DocumentClassification.None => true,
        DocumentClassification.Leadership => MayClassified,
        DocumentClassification.Tru => MayClassified || IsTru,
        DocumentClassification.Hrb => MayClassified || IsHrb,
        _ => false,
    };

    /// <summary>Die Verschluss-Stufen, die der Agent vergeben darf (für die Auswahl im Editor/Upload-Dialog).
    /// Führung darf jede Stufe setzen; TRU-/HRB-Angehörige nur die eigene; „Keine" steht jedem offen.</summary>
    public static IReadOnlyList<DocumentClassification> AssignableOptions(ClaimsPrincipal user)
    {
        var options = new List<DocumentClassification> { DocumentClassification.None };
        if (user.IsLeadership())
        {
            options.Add(DocumentClassification.Leadership);
        }
        if (user.IsLeadership() || user.IsTRU())
        {
            options.Add(DocumentClassification.Tru);
        }
        if (user.IsLeadership() || user.IsHRB())
        {
            options.Add(DocumentClassification.Hrb);
        }
        return options;
    }
}
