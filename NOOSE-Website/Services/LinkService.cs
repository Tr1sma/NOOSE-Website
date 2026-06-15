using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IVerknuepfungService" />
public class LinkService(IDbContextFactory<AppDbContext> dbFactory, IThreatScoreService threat) : ILinkService
{
    public async Task<List<LinkDisplay>> GetForRecordAsync(string entityType, string entityId, bool isLeadership, string? meId, LinkKind? kind = null, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // meId für die Taskforce-Mitgliedschafts-Sichtbarkeit (sowohl der Akte selbst als auch verknüpfter Taskforces).
        if (!await Visibility.IsRecordVisibleAsync(db, entityType, entityId, isLeadership, cancellationToken, meId))
        {
            return new();
        }

        var query = db.Links
            .Where(v => (v.SourceType == entityType && v.SourceId == entityId)
                     || (v.TargetType == entityType && v.TargetId == entityId));
        if (kind is not null)
        {
            query = query.Where(v => v.Kind == kind.Value);
        }
        var raw = await query
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);

        // Je Verknüpfung die „andere Seite" relativ zur betrachteten Akte bestimmen.
        var pairs = raw.Select(v =>
        {
            var isBy = v.SourceType == entityType && v.SourceId == entityId;
            return (V: v,
                    OtherType: isBy ? v.TargetType : v.SourceType,
                    OtherId: isBy ? v.TargetId : v.SourceId);
        }).ToList();

        // Ziele für Anzeige + Sichtbarkeit + Navigations-Href auflösen – je Typ eine Sammelabfrage, dann
        // in EINE Lookup-Map (Typ, Id) → (Bezeichnung, Verschlusssache, Href) zusammenführen.
        var targets = new Dictionary<(string Type, string Id), (string Designation, bool Classified, string? Href)>();

        var personIds = pairs.Where(p => p.OtherType == nameof(Person)).Select(p => p.OtherId).Distinct().ToList();
        foreach (var x in await db.People.Where(p => personIds.Contains(p.Id))
                     .Select(p => new { p.Id, p.Name, p.CaseNumber, p.IsClassified }).ToListAsync(cancellationToken))
        {
            targets[(nameof(Person), x.Id)] = ($"{x.Name} ({x.CaseNumber})", x.IsClassified, $"/personen/{x.Id}");
        }

        var factionIds = pairs.Where(p => p.OtherType == nameof(Faction)).Select(p => p.OtherId).Distinct().ToList();
        foreach (var x in await db.Factions.Where(f => factionIds.Contains(f.Id))
                     .Select(f => new { f.Id, f.Name, f.CaseNumber, f.IsClassified }).ToListAsync(cancellationToken))
        {
            targets[(nameof(Faction), x.Id)] = ($"{x.Name} ({x.CaseNumber})", x.IsClassified, $"/fraktionen/{x.Id}");
        }

        var groupsIds = pairs.Where(p => p.OtherType == nameof(PersonGroup)).Select(p => p.OtherId).Distinct().ToList();
        foreach (var x in await db.PersonGroups.Where(g => groupsIds.Contains(g.Id))
                     .Select(g => new { g.Id, g.Name, g.CaseNumber, g.IsClassified }).ToListAsync(cancellationToken))
        {
            targets[(nameof(PersonGroup), x.Id)] = ($"{x.Name} ({x.CaseNumber})", x.IsClassified, $"/personengruppen/{x.Id}");
        }

        var partyIds = pairs.Where(p => p.OtherType == nameof(Party)).Select(p => p.OtherId).Distinct().ToList();
        foreach (var x in await db.Parties.Where(p => partyIds.Contains(p.Id))
                     .Select(p => new { p.Id, p.Name, p.CaseNumber, p.IsClassified }).ToListAsync(cancellationToken))
        {
            targets[(nameof(Party), x.Id)] = ($"{x.Name} ({x.CaseNumber})", x.IsClassified, $"/parteien/{x.Id}");
        }

        // Operation (Phase 5b): aufgelöst mit Navigation auf die eigene Detailseite.
        var operationIds = pairs.Where(p => p.OtherType == nameof(Operation)).Select(p => p.OtherId).Distinct().ToList();
        foreach (var x in await db.Operations.Where(o => operationIds.Contains(o.Id))
                     .Select(o => new { o.Id, o.Title, o.CaseNumber, o.IsClassified }).ToListAsync(cancellationToken))
        {
            targets[(nameof(Operation), x.Id)] = ($"{x.Title} ({x.CaseNumber})", x.IsClassified, $"/operationen/{x.Id}");
        }

        // Taskforce (Phase 5c): nur die für den Betrachter sichtbaren (zugeteilt oder darf alle) auflösen –
        // fremde Taskforces bleiben unaufgelöst und werden so aus der Beziehungs-Anzeige ausgeblendet.
        var taskforceIds = pairs.Where(p => p.OtherType == nameof(Taskforce)).Select(p => p.OtherId).Distinct().ToList();
        var visibleTf = await TaskforceVisibility.VisibleIdsAsync(db, taskforceIds, isLeadership, meId, cancellationToken);
        foreach (var x in await db.Taskforces.Where(t => visibleTf.Contains(t.Id))
                     .Select(t => new { t.Id, t.Name, t.CaseNumber, t.IsClassified }).ToListAsync(cancellationToken))
        {
            targets[(nameof(Taskforce), x.Id)] = ($"{x.Name} ({x.CaseNumber})", x.IsClassified, $"/taskforces/{x.Id}");
        }

        // Vorgang (Phase 5): aufgelöst mit Navigation auf die eigene Detailseite.
        var caseIds = pairs.Where(p => p.OtherType == nameof(Case)).Select(p => p.OtherId).Distinct().ToList();
        foreach (var x in await db.Cases.Where(v => caseIds.Contains(v.Id))
                     .Select(v => new { v.Id, v.Title, v.CaseNumber, v.IsClassified }).ToListAsync(cancellationToken))
        {
            targets[(nameof(Case), x.Id)] = ($"{x.Title} ({x.CaseNumber})", x.IsClassified, $"/vorgaenge/{x.Id}");
        }

        // Aufgabe (Phase 6): kein Verschlusssache-Konzept (Team-Board); Navigation auf die eigene Detailseite.
        var jobIds = pairs.Where(p => p.OtherType == nameof(Job)).Select(p => p.OtherId).Distinct().ToList();
        foreach (var x in await db.Jobs.Where(a => jobIds.Contains(a.Id))
                     .Select(a => new { a.Id, a.Title, a.CaseNumber }).ToListAsync(cancellationToken))
        {
            targets[(nameof(Job), x.Id)] = ($"{x.Title} ({x.CaseNumber})", false, $"/aufgaben/{x.Id}");
        }

        // Agent: kein Verschlusssache-Konzept und keine eigene Detailseite (Href null) – nur Codename.
        var agentIds = pairs.Where(p => p.OtherType == nameof(Agent)).Select(p => p.OtherId).Distinct().ToList();
        foreach (var x in await db.Users.Where(u => agentIds.Contains(u.Id))
                     .Select(u => new { u.Id, u.Codename }).ToListAsync(cancellationToken))
        {
            targets[(nameof(Agent), x.Id)] = (string.IsNullOrWhiteSpace(x.Codename) ? "(unbenannter Agent)" : x.Codename, false, null);
        }

        // Personen-Dok: erbt Sichtbarkeit von seiner Person (Join auf Personen → Soft-Delete/Verschlusssache
        // greifen automatisch); Navigation auf die Personen-Akte, Doks-Tab.
        var docIds = pairs.Where(p => p.OtherType == nameof(PersonDoc)).Select(p => p.OtherId).Distinct().ToList();
        foreach (var x in await db.PersonDocs.Where(d => docIds.Contains(d.Id))
                     .Join(db.People, d => d.PersonId, p => p.Id,
                           (d, p) => new { d.Id, d.Timestamp, PersonId = p.Id, PersonName = p.Name, p.IsClassified })
                     .ToListAsync(cancellationToken))
        {
            targets[(nameof(PersonDoc), x.Id)] = ($"Dok – {x.PersonName} ({x.Timestamp.ToLocalTime():dd.MM.yyyy})", x.IsClassified, $"/personen/{x.PersonId}?tab=doks");
        }

        // Observation (Phase 5): erbt Sichtbarkeit von seiner Person (Join auf Personen → Soft-Delete/
        // Verschlusssache greifen automatisch); Navigation auf die Personen-Akte, Observationen-Tab.
        var observationIds = pairs.Where(p => p.OtherType == nameof(Observation)).Select(p => p.OtherId).Distinct().ToList();
        foreach (var x in await db.Observations.Where(o => observationIds.Contains(o.Id))
                     .Join(db.People, o => o.PersonId, p => p.Id,
                           (o, p) => new { o.Id, o.Start, PersonId = p.Id, PersonName = p.Name, p.IsClassified })
                     .ToListAsync(cancellationToken))
        {
            targets[(nameof(Observation), x.Id)] = ($"Observation – {x.PersonName} ({x.Start.ToLocalTime():dd.MM.yyyy})", x.IsClassified, $"/personen/{x.PersonId}?tab=ueberwachung");
        }

        // Gesetz (Phase 7): kein Verschlusssache-Konzept (Wissensbasis); Navigation auf die Detailseite.
        var lawIds = pairs.Where(p => p.OtherType == nameof(Law)).Select(p => p.OtherId).Distinct().ToList();
        foreach (var x in await db.Laws.Where(g => lawIds.Contains(g.Id))
                     .Select(g => new { g.Id, g.Paragraph, g.Title, g.LawBook }).ToListAsync(cancellationToken))
        {
            targets[(nameof(Law), x.Id)] = ($"{x.Paragraph} {x.Title} ({x.LawBook})", false, $"/gesetze/{x.Id}");
        }

        var knownTypes = new[]
        {
            nameof(Person), nameof(Faction), nameof(PersonGroup), nameof(Party),
            nameof(Operation), nameof(Taskforce), nameof(Case), nameof(Agent),
            nameof(PersonDoc), nameof(Observation), nameof(Job), nameof(Law),
        };
        var result = new List<LinkDisplay>();
        foreach (var p in pairs)
        {
            if (targets.TryGetValue((p.OtherType, p.OtherId), out var info))
            {
                // Verschlusssache nur für die Führung sichtbar.
                if (info.Classified && !isLeadership)
                {
                    continue;
                }
                result.Add(new LinkDisplay(p.V.Id, p.OtherType, p.OtherId, p.V.Label, info.Designation, p.V.Automatic, info.Href));
            }
            else if (knownTypes.Contains(p.OtherType))
            {
                // Bekannter Aktentyp, aber nicht aufgelöst → Ziel im Papierkorb/unbekannt → ausblenden.
            }
            else
            {
                // Unbekannter Aktentyp → vorerst Rohbezeichnung ohne Navigationsziel.
                result.Add(new LinkDisplay(p.V.Id, p.OtherType, p.OtherId, p.V.Label, p.OtherId, p.V.Automatic));
            }
        }
        return result;
    }

    public async Task CreateAsync(string sourceType, string sourceId, string targetType, string targetId, string? label, ClaimsPrincipal actor, LinkKind kind = LinkKind.Default, CancellationToken cancellationToken = default)
    {
        if (sourceType == targetType && sourceId == targetId)
        {
            throw new InvalidOperationException("Eine Akte kann nicht mit sich selbst verknüpft werden.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Härtung: nicht manuell auf ein Ziel verlinken, das der Handelnde nicht sehen darf (Verschlusssache).
        // Die Picker (VerknuepfungDialog) zeigen ohnehin nur sichtbare Ziele – das hier schließt geschmiedete
        // Direktaufrufe. Nur die VS-fähigen Aktentypen prüfen; Agent/PersonDok/Observation/Aufgabe bleiben
        // unverändert verlinkbar (deren Sichtbarkeit regelt der jeweilige Anzeige-/Lesepfad).
        if (targetType is nameof(Person) or nameof(Faction) or nameof(PersonGroup) or nameof(Party)
                or nameof(Operation) or nameof(Taskforce) or nameof(Case)
            && !await Visibility.IsRecordVisibleAsync(db, targetType, targetId, actor.IsLeadership(), cancellationToken))
        {
            throw new UnauthorizedAccessException("Auf diese Akte darfst du nicht verlinken (Verschlusssache oder nicht vorhanden).");
        }

        // Doppelte (aktive) Verknüpfung derselben Art verhindern – in beiden Richtungen. Soft-gelöschte
        // (Papierkorb) sind durch den globalen Filter ausgenommen und blockieren das Neuanlegen nicht.
        var exists = await db.Links.AnyAsync(v => v.Kind == kind
            && ((v.SourceType == sourceType && v.SourceId == sourceId && v.TargetType == targetType && v.TargetId == targetId)
             || (v.SourceType == targetType && v.SourceId == targetId && v.TargetType == sourceType && v.TargetId == sourceId)),
            cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("Diese Verknüpfung besteht bereits.");
        }

        db.Links.Add(new Link
        {
            SourceType = sourceType,
            SourceId = sourceId,
            TargetType = targetType,
            TargetId = targetId,
            Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
            Kind = kind,
        });
        await db.SaveChangesAsync(cancellationToken);

        // Jede manuelle Verknüpfung mit Fraktionsbeteiligung wirkt auf deren Score: Konflikt/Bündnis auf S3,
        // Standard auf S4 (Netzwerk-Zentralität) → neu berechnen (no-op, falls keine Seite eine Fraktion ist).
        await ThreatNewCalculateAsync(sourceType, sourceId, targetType, targetId, cancellationToken);
    }

    /// <summary>Berechnet den Bedrohungs-Score jeder an der Verknüpfung beteiligten Fraktion neu (no-op für Nicht-Fraktionen).</summary>
    private async Task ThreatNewCalculateAsync(string sourceType, string sourceId, string targetType, string targetId, CancellationToken cancellationToken)
    {
        if (sourceType == nameof(Faction))
        {
            await threat.NewCalculateAsync(sourceId, cancellationToken);
        }
        if (targetType == nameof(Faction))
        {
            await threat.NewCalculateAsync(targetId, cancellationToken);
        }
        // Person-inzidente Standard-Verknüpfungen fließen in den Person-Score (P5).
        if (sourceType == nameof(Person))
        {
            await threat.NewCalculatePersonScoreAsync(sourceId, cancellationToken);
        }
        if (targetType == nameof(Person))
        {
            await threat.NewCalculatePersonScoreAsync(targetId, cancellationToken);
        }
    }

    public async Task RemoveAsync(string linkId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var v = await db.Links.FirstOrDefaultAsync(x => x.Id == linkId, cancellationToken);
        if (v is null)
        {
            return;
        }
        db.Links.Remove(v); // Soft-Delete via Interceptor
        await db.SaveChangesAsync(cancellationToken);

        // Entfernte manuelle Verknüpfung wirkt auf S3 (Konflikt/Bündnis) oder S4 (Standard) der beteiligten Fraktionen.
        await ThreatNewCalculateAsync(v.SourceType, v.SourceId, v.TargetType, v.TargetId, cancellationToken);
    }
}
