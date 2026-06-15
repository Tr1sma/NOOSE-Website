using System.Security.Claims;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>
/// Generische Verknüpfungs-Engine: legt gerichtete Verknüpfungen zwischen beliebigen Akten an und liefert
/// sie aus Sicht einer Akte <b>bidirektional</b> normalisiert („andere Seite"). Ziele sind Personen,
/// Fraktionen oder Personengruppen; Verschlusssache-/Papierkorb-Sichtbarkeit wird über die jeweilige Akte
/// geprüft. Über <see cref="VerknuepfungArt"/> getrennt: allgemeine Verknüpfungen vs. Konflikte/Bündnisse.
/// </summary>
public interface ILinkService
{
    /// <summary>Verknüpfungen einer Akte; mit <paramref name="art"/> auf eine Beziehungsart eingeschränkt (null = alle).
    /// <paramref name="meId"/> = Agent-Id des Betrachters (verknüpfte fremde Taskforces werden ausgeblendet).</summary>
    Task<List<LinkDisplay>> GetForRecordAsync(string entityType, string entityId, bool isLeadership, string? meId, LinkKind? kind = null, CancellationToken cancellationToken = default);

    Task CreateAsync(string sourceType, string sourceId, string targetType, string targetId, string? label, ClaimsPrincipal actor, LinkKind kind = LinkKind.Default, CancellationToken cancellationToken = default);

    Task RemoveAsync(string linkId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
