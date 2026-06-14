using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IPersonMergeService" />
public class PersonMergeService(IDbContextFactory<AppDbContext> dbFactory) : IPersonMergeService
{
    public async Task MergeAsync(string sourceId, string targetId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        // ExecuteUpdate umgeht den SaveChanges-Interceptor → die Nur-Lese-Aufsicht hier explizit sperren.
        Permission.RequireWriteAccess(actor);

        if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetId) || sourceId == targetId)
        {
            throw new InvalidOperationException("Bitte zwei unterschiedliche Akten wählen.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var source = await db.People
            .Include(p => p.Aliases).Include(p => p.PhoneNumbers).Include(p => p.Vehicles)
            .Include(p => p.Locations).Include(p => p.Weapons)
            .FirstOrDefaultAsync(p => p.Id == sourceId, cancellationToken)
            ?? throw new InvalidOperationException("Die Quell-Akte wurde nicht gefunden.");
        var target = await db.People
            .Include(p => p.Aliases).Include(p => p.PhoneNumbers).Include(p => p.Vehicles)
            .Include(p => p.Locations).Include(p => p.Weapons)
            .FirstOrDefaultAsync(p => p.Id == targetId, cancellationToken)
            ?? throw new InvalidOperationException("Die Ziel-Akte wurde nicht gefunden.");

        // ---- Kind-Daten ohne Dubletten-Risiko: komplett umhängen (Bulk, umgeht das Change-Tracking
        //      der geladenen Person-Children nicht – Doks/Fotos/Observationen sind nicht geladen). ----
        await db.PersonDocs.Where(d => d.PersonId == sourceId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.PersonId, targetId), cancellationToken);
        await db.Observations.Where(o => o.PersonId == sourceId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.PersonId, targetId), cancellationToken);
        await db.PersonPhotos.Where(f => f.PersonId == sourceId)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.PersonId, targetId), cancellationToken);

        // ---- Steckbrief-Kinder mit Dubletten-Abgleich (case-insensitiv über Trim/Lower). ----
        static string Norm(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();

        var targetAliases = target.Aliases.Select(a => Norm(a.AliasName)).ToHashSet();
        targetAliases.Add(Norm(target.Name));
        foreach (var alias in source.Aliases)
        {
            if (targetAliases.Add(Norm(alias.AliasName)))
            {
                alias.PersonId = targetId;
            }
            else
            {
                db.PersonAliases.Remove(alias);
            }
        }
        // Der Name der Quell-Akte bleibt als Alias auffindbar.
        if (targetAliases.Add(Norm(source.Name)))
        {
            db.PersonAliases.Add(new PersonAlias { PersonId = targetId, AliasName = source.Name });
        }

        var targetPhones = target.PhoneNumbers.Select(t => Norm(t.Number)).ToHashSet();
        foreach (var phone in source.PhoneNumbers)
        {
            if (targetPhones.Add(Norm(phone.Number)))
            {
                phone.PersonId = targetId;
            }
            else
            {
                db.PersonPhones.Remove(phone);
            }
        }

        var targetVehicles = target.Vehicles.Select(f => Norm(f.Designation) + "|" + Norm(f.LicensePlate)).ToHashSet();
        foreach (var vehicle in source.Vehicles)
        {
            if (targetVehicles.Add(Norm(vehicle.Designation) + "|" + Norm(vehicle.LicensePlate)))
            {
                vehicle.PersonId = targetId;
            }
            else
            {
                db.PersonVehicles.Remove(vehicle);
            }
        }

        var targetLocations = target.Locations.Select(o => Norm(o.Text)).ToHashSet();
        foreach (var location in source.Locations)
        {
            if (targetLocations.Add(Norm(location.Text)))
            {
                location.PersonId = targetId;
            }
            else
            {
                db.PersonLocations.Remove(location);
            }
        }

        var targetWeapons = target.Weapons.Select(w => Norm(w.Text)).ToHashSet();
        foreach (var weapon in source.Weapons)
        {
            if (targetWeapons.Add(Norm(weapon.Text)))
            {
                weapon.PersonId = targetId;
            }
            else
            {
                db.PersonWeapons.Remove(weapon);
            }
        }

        // ---- Person-zu-Person-Beziehungen: umhängen; Selbstbezüge entfernen. ----
        var relations = await db.PersonRelations
            .Where(b => b.PersonAId == sourceId || b.PersonBId == sourceId)
            .ToListAsync(cancellationToken);
        foreach (var relation in relations)
        {
            if (relation.PersonAId == sourceId)
            {
                relation.PersonAId = targetId;
            }
            if (relation.PersonBId == sourceId)
            {
                relation.PersonBId = targetId;
            }
            if (relation.PersonAId == relation.PersonBId)
            {
                db.PersonRelations.Remove(relation);
            }
        }

        // ---- Mitgliedschaften (aktive): umhängen, außer das Ziel ist in derselben Akte bereits Mitglied. ----
        var targetFactions = await db.FactionMembers.Where(m => m.PersonId == targetId)
            .Select(m => m.FactionId).ToListAsync(cancellationToken);
        foreach (var member in await db.FactionMembers.Where(m => m.PersonId == sourceId).ToListAsync(cancellationToken))
        {
            if (targetFactions.Contains(member.FactionId))
            {
                db.FactionMembers.Remove(member);
            }
            else
            {
                member.PersonId = targetId;
            }
        }

        var targetGroups = await db.PersonGroupMembers.Where(m => m.PersonId == targetId)
            .Select(m => m.PersonGroupId).ToListAsync(cancellationToken);
        foreach (var member in await db.PersonGroupMembers.Where(m => m.PersonId == sourceId).ToListAsync(cancellationToken))
        {
            if (targetGroups.Contains(member.PersonGroupId))
            {
                db.PersonGroupMembers.Remove(member);
            }
            else
            {
                member.PersonId = targetId;
            }
        }

        var targetParties = await db.PartyMembers.Where(m => m.PersonId == targetId)
            .Select(m => m.PartyId).ToListAsync(cancellationToken);
        foreach (var member in await db.PartyMembers.Where(m => m.PersonId == sourceId).ToListAsync(cancellationToken))
        {
            if (targetParties.Contains(member.PartyId))
            {
                db.PartyMembers.Remove(member);
            }
            else
            {
                member.PersonId = targetId;
            }
        }

        // ---- Polymorphe Bezüge (EntitaetTyp/-Id == Person/quelleId) umhängen. ----
        const string type = nameof(Person);

        // Einstufungs-Verlauf (append-only) – die Historie beider Akten wird zusammengeführt.
        await db.ClassificationHistory.Where(e => e.EntityType == type && e.EntityId == sourceId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.EntityId, targetId), cancellationToken);

        // Kommentare, Quellen, Wiedervorlagen: konfliktfrei umhängen.
        await db.Comments.Where(k => k.EntityType == type && k.EntityId == sourceId)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.EntityId, targetId), cancellationToken);
        await db.Sources.Where(q => q.EntityType == type && q.EntityId == sourceId)
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.EntityId, targetId), cancellationToken);
        await db.Sources.Where(q => q.TargetType == type && q.TargetId == sourceId)
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.TargetId, targetId), cancellationToken);
        await db.Followups.Where(w => w.EntityType == type && w.EntityId == sourceId)
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.EntityId, targetId), cancellationToken);

        // Tags: Unique-Index (TagId, Typ, Id) → nur umhängen, was das Ziel noch nicht trägt.
        var targetTagIds = await db.TagMappings.Where(z => z.EntityType == type && z.EntityId == targetId)
            .Select(z => z.TagId).ToListAsync(cancellationToken);
        foreach (var mapping in await db.TagMappings.Where(z => z.EntityType == type && z.EntityId == sourceId).ToListAsync(cancellationToken))
        {
            if (targetTagIds.Contains(mapping.TagId))
            {
                db.TagMappings.Remove(mapping);
            }
            else
            {
                mapping.EntityId = targetId;
            }
        }

        // Custom-Felder: Unique-Index je Definition → vorhandene Ziel-Werte haben Vorrang.
        var targetFieldIds = await db.CustomFieldValues.Where(w => w.EntityType == type && w.EntityId == targetId)
            .Select(w => w.CustomFieldDefinitionId).ToListAsync(cancellationToken);
        foreach (var value in await db.CustomFieldValues.Where(w => w.EntityType == type && w.EntityId == sourceId).ToListAsync(cancellationToken))
        {
            if (targetFieldIds.Contains(value.CustomFieldDefinitionId))
            {
                db.CustomFieldValues.Remove(value);
            }
            else
            {
                value.EntityId = targetId;
            }
        }

        // Watchlist: je Agent nur ein aktiver Eintrag pro Akte.
        var targetFollower = await db.Watchlists.Where(w => w.EntityType == type && w.EntityId == targetId)
            .Select(w => w.AgentId).ToListAsync(cancellationToken);
        foreach (var entry in await db.Watchlists.Where(w => w.EntityType == type && w.EntityId == sourceId).ToListAsync(cancellationToken))
        {
            if (targetFollower.Contains(entry.AgentId))
            {
                db.Watchlists.Remove(entry);
            }
            else
            {
                entry.EntityId = targetId;
            }
        }

        // Verknüpfungen: beide Seiten umhängen; entstehende Selbst-Verknüpfungen entfernen.
        foreach (var link in await db.Links
                     .Where(v => (v.SourceType == type && v.SourceId == sourceId) || (v.TargetType == type && v.TargetId == sourceId))
                     .ToListAsync(cancellationToken))
        {
            if (link.SourceType == type && link.SourceId == sourceId)
            {
                link.SourceId = targetId;
            }
            if (link.TargetType == type && link.TargetId == sourceId)
            {
                link.TargetId = targetId;
            }
            if (link.SourceType == link.TargetType && link.SourceId == link.TargetId)
            {
                db.Links.Remove(link);
            }
        }

        // Offene Anträge (z. B. Hochstufung): auf die Ziel-Akte umbiegen, Bezeichnung aktualisieren.
        foreach (var request in await db.Requests
                     .Where(a => a.TargetType == type && a.TargetId == sourceId)
                     .ToListAsync(cancellationToken))
        {
            request.TargetId = targetId;
            request.TargetDesignation = $"{target.Name} ({target.CaseNumber})";
        }

        // ---- Steckbrief: fehlende Angaben der Ziel-Akte aus der Quelle übernehmen. ----
        if (string.IsNullOrWhiteSpace(target.Description) && !string.IsNullOrWhiteSpace(source.Description))
        {
            target.Description = source.Description;
        }
        // Verschlusssache „färbt ab": die zusammengeführte Akte enthält auch die VS-Inhalte der Quelle.
        target.IsClassified = target.IsClassified || source.IsClassified;
        // Einstufung/Lebensstatus der Ziel-Akte bleiben bewusst unangetastet (Rang-Gate der Einstufung).

        // Nachvollziehbarkeit direkt an der Ziel-Akte (zusätzlich zum Audit-Log).
        db.Comments.Add(new Comment
        {
            EntityType = type,
            EntityId = targetId,
            Text = $"Akte „{source.Name}“ ({source.CaseNumber}) wurde in diese Akte überführt (Duplikat-Zusammenführung).",
            AuthorName = actor.GetCodename(),
        });

        // ---- Quell-Akte in den Papierkorb (Interceptor wandelt Remove in Soft-Delete um). ----
        db.People.Remove(source);

        await db.SaveChangesAsync(cancellationToken);
    }
}
