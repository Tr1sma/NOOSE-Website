using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.People;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IPersonDokService" />
public class PersonDocService(IDbContextFactory<AppDbContext> dbFactory, IPersonService personService, IThreatScoreService threat) : IPersonDocService
{
    public async Task<List<PersonDocDisplay>> GetForPersonAsync(string personId, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Eigenständige Sichtbarkeitsprüfung der Eltern-Person (nicht nur auf den Aufrufer verlassen).
        if (!await Visibility.IsRecordVisibleAsync(db, nameof(Person), personId, scope, cancellationToken))
        {
            return new();
        }
        var docs = await db.PersonDocs
            .Where(d => d.PersonId == personId)
            .OrderByDescending(d => d.Timestamp)
            .ToListAsync(cancellationToken);
        if (scope.PartnerAgency is { } agency)
        {
            docs = await PartnerVisibility.FilterChildrenAsync(db, nameof(Person), personId, nameof(PersonDoc), docs, d => d.Id, agency, scope.MeId, cancellationToken);
        }
        return await ToDisplayAsync(db, docs, scope.MayClassifiedRead, cancellationToken);
    }

    public async Task<List<PersonDocDisplay>> GetForOrgAsync(string orgType, string orgId, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // check org record visibility
        if (!await Visibility.IsRecordVisibleAsync(db, orgType, orgId, scope, cancellationToken))
        {
            return new();
        }
        var docs = await db.PersonDocs
            .Include(d => d.Person)
            // null person trashed; classified leadership-only
            .Where(d => d.OrgType == orgType && d.OrgId == orgId
                && d.Person != null && (scope.MayClassifiedRead || !d.Person.IsClassified))
            .OrderByDescending(d => d.Timestamp)
            .ToListAsync(cancellationToken);
        if (scope.PartnerAgency is { } agency)
        {
            // partners: only released persons
            var released = await PartnerVisibility.ReleasedParentIdsAsync(db, nameof(Person), docs.Select(d => d.PersonId).Distinct().ToList(), agency, scope.MeId, cancellationToken);
            docs = docs.Where(d => released.Contains(d.PersonId)).ToList();
        }
        return await ToDisplayAsync(db, docs, scope.MayClassifiedRead, cancellationToken);
    }

    public async Task<List<PersonDocDisplay>> GetAllAsync(bool isLeadership, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var docs = await db.PersonDocs
            .Include(d => d.Person)
            // Der Soft-Delete-Filter setzt Person bei gelöschten Akten auf null → solche Doks ausblenden.
            .Where(d => d.Person != null && (isLeadership || !d.Person.IsClassified))
            .OrderByDescending(d => d.Timestamp)
            .ToListAsync(cancellationToken);
        return await ToDisplayAsync(db, docs, isLeadership, cancellationToken);
    }

    /// <summary>
    /// Reichert geladene Doks mit den Anzeigedaten ihrer verknüpften Organisation an. Namen werden in je
    /// einer Sammelabfrage je Org-Typ aufgelöst und dabei Verschlusssache-gefiltert (<paramref name="istFuehrung"/>);
    /// der globale Soft-Delete-Filter blendet gelöschte Orgs automatisch aus. Nicht (mehr) sichtbare oder
    /// nicht verknüpfte Doks erhalten leere Org-Felder → die Anzeige fällt auf den Freitext zurück.
    /// </summary>
    private static async Task<List<PersonDocDisplay>> ToDisplayAsync(AppDbContext db, List<PersonDoc> docs, bool isLeadership, CancellationToken cancellationToken)
    {
        var factionIds = docs.Where(d => d.OrgType == nameof(Faction) && d.OrgId is not null).Select(d => d.OrgId!).Distinct().ToList();
        var groupsIds = docs.Where(d => d.OrgType == nameof(PersonGroup) && d.OrgId is not null).Select(d => d.OrgId!).Distinct().ToList();

        var factions = new Dictionary<string, (string Name, string CaseNumber)>();
        if (factionIds.Count > 0)
        {
            factions = await db.Factions
                .Where(f => factionIds.Contains(f.Id) && (isLeadership || !f.IsClassified))
                .Select(f => new { f.Id, f.Name, f.CaseNumber })
                .ToDictionaryAsync(f => f.Id, f => (f.Name, f.CaseNumber), cancellationToken);
        }

        var groups = new Dictionary<string, (string Name, string CaseNumber)>();
        if (groupsIds.Count > 0)
        {
            groups = await db.PersonGroups
                .Where(g => groupsIds.Contains(g.Id) && (isLeadership || !g.IsClassified))
                .Select(g => new { g.Id, g.Name, g.CaseNumber })
                .ToDictionaryAsync(g => g.Id, g => (g.Name, g.CaseNumber), cancellationToken);
        }

        return docs.Select(d =>
        {
            if (d.OrgId is not null && d.OrgType == nameof(Faction) && factions.TryGetValue(d.OrgId, out var f))
            {
                return new PersonDocDisplay(d, f.Name, f.CaseNumber, $"/fraktionen/{d.OrgId}");
            }
            if (d.OrgId is not null && d.OrgType == nameof(PersonGroup) && groups.TryGetValue(d.OrgId, out var g))
            {
                return new PersonDocDisplay(d, g.Name, g.CaseNumber, $"/personengruppen/{d.OrgId}");
            }
            return new PersonDocDisplay(d, null, null, null);
        }).ToList();
    }

    public async Task<PersonDoc> CreateAsync(string personId, PersonDocInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var person = await db.People.FirstOrDefaultAsync(p => p.Id == personId, cancellationToken)
            ?? throw new InvalidOperationException($"Person '{personId}' nicht gefunden.");
        if (person.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        var doc = await CreateDocAsync(db, personId, input, cancellationToken);
        // Maßnahmen fließen in den Heat der Fraktionen der Person (S1) UND in den Person-Score selbst (P1).
        await threat.NewCalculateForPersonAsync(personId, cancellationToken);
        await threat.NewCalculatePersonScoreAsync(personId, cancellationToken);
        return doc;
    }

    public async Task<PersonDoc> CreateForNewPersonAsync(string name, PersonDocInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Für eine neue Akte ist ein Name erforderlich.");
        }

        // Neue Akte (nur Name) über den Personen-Dienst anlegen – inkl. Aktenzeichen-Vergabe und Audit –
        // und das Dok daran hängen. Jeder Dienst nutzt seinen eigenen Context aus der Factory; die Person
        // ist nach ErstellenAsync committet und wird unten in unserem Context frisch geladen.
        var person = await personService.CreateAsync(new PersonInput { Name = name.Trim() }, actor, cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var doc = await CreateDocAsync(db, person.Id, input, cancellationToken);
        await threat.NewCalculateForPersonAsync(person.Id, cancellationToken);
        await threat.NewCalculatePersonScoreAsync(person.Id, cancellationToken);
        return doc;
    }

    public async Task<PersonDoc> RefreshAsync(string docId, PersonDocInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var doc = await db.PersonDocs
            .Include(d => d.Person)
            .FirstOrDefaultAsync(d => d.Id == docId, cancellationToken)
            ?? throw new InvalidOperationException($"Dok '{docId}' nicht gefunden.");

        if (doc.Person?.IsClassified == true && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        // Alten Zustand merken, bevor wir überschreiben – die Status-Neuauswertung braucht beides.
        var altOutcome = doc.Outcome;
        var altTimestamp = doc.Timestamp;

        doc.Timestamp = input.Timestamp;
        doc.Reason = Empty(input.Reason);
        doc.Faction = Empty(input.Faction);
        var aktOrgId = Empty(input.OrgId);
        doc.OrgId = aktOrgId;
        // Kein verwaister Typ ohne Id (Freitext-Fallback).
        doc.OrgType = aktOrgId is null ? null : Empty(input.OrgType);
        doc.ReceivedInformation = Empty(input.ReceivedInformation);
        doc.TruthSerum = input.TruthSerum;
        doc.Outcome = input.Outcome;
        // Gedächtnisverlust folgt dem Ausgang (Amnestie-Spritze).
        doc.MemoryDeleted = input.Outcome == MeasureOutcome.Injection;

        // Person ist null, wenn ihre Akte (soft-)gelöscht ist – dann ist der Lebensstatus ohnehin belanglos.
        if (doc.Person is not null)
        {
            StatusNewEvaluate(doc.Person, altOutcome, altTimestamp, input);
        }

        // Dok + ggf. Person in einem SaveChanges → Audit setzt GeaendertAm/Von automatisch.
        await db.SaveChangesAsync(cancellationToken);
        // Geänderter Ausgang/Zeitpunkt wirkt auf den Heat (S1) der Fraktionen der Person UND den Person-Score (P1).
        await threat.NewCalculateForPersonAsync(doc.PersonId, cancellationToken);
        await threat.NewCalculatePersonScoreAsync(doc.PersonId, cancellationToken);
        return doc;
    }

    /// <summary>
    /// Wertet den Status-Effekt eines bearbeiteten Doks neu aus („Status neu anwenden"). Ein Dok „besitzt"
    /// das aktuelle Tot-Fenster nur, wenn dieses aus seinem (alten) Zeitpunkt stammt – so wird ein manuell
    /// oder von einem anderen Dok gesetzter Lebensstatus nicht überschrieben.
    /// </summary>
    private static void StatusNewEvaluate(Person person, MeasureOutcome altOutcome, DateTime altTimestamp, PersonDocInput @new)
    {
        var altWasShot = altOutcome == MeasureOutcome.Shot;
        var newIsShot = @new.Outcome == MeasureOutcome.Shot;
        var ownsWindow = person.LifeStatus == LifeStatus.Dead
            && person.DeadUntil == LifeStatusLogic.DeadUntilFrom(altTimestamp);

        if (newIsShot)
        {
            if (!altWasShot)
            {
                // Neu „Erschossen": Tod zum Maßnahme-Zeitpunkt setzen (wie beim Anlegen).
                person.LifeStatus = LifeStatus.Dead;
                person.DeadUntil = LifeStatusLogic.DeadUntilFrom(@new.Timestamp);
            }
            else if (ownsWindow)
            {
                // Bleibt „Erschossen", Zeitpunkt evtl. verschoben → eigenes Tot-Fenster nachführen.
                person.DeadUntil = LifeStatusLogic.DeadUntilFrom(@new.Timestamp);
            }
        }
        else if (altWasShot && ownsWindow)
        {
            // Weg von „Erschossen": das von diesem Dok gesetzte Tot-Fenster zurücknehmen.
            person.LifeStatus = LifeStatus.Alive;
            person.DeadUntil = null;
        }
    }

    private async Task<PersonDoc> CreateDocAsync(AppDbContext db, string personId, PersonDocInput input, CancellationToken cancellationToken)
    {
        var orgId = Empty(input.OrgId);
        var doc = new PersonDoc
        {
            PersonId = personId,
            Timestamp = input.Timestamp,
            Reason = Empty(input.Reason),
            Faction = Empty(input.Faction),
            OrgId = orgId,
            // Kein verwaister Typ ohne Id (Freitext-Fallback).
            OrgType = orgId is null ? null : Empty(input.OrgType),
            ReceivedInformation = Empty(input.ReceivedInformation),
            TruthSerum = input.TruthSerum,
            Outcome = input.Outcome,
        };

        // Automatik: Maßnahme-Ausgang wirkt auf den Lebensstatus der Person.
        switch (input.Outcome)
        {
            case MeasureOutcome.Shot:
                // Tod tritt zum Maßnahme-Zeitpunkt ein; 20-Minuten-Fenster bis zum Respawn. Die Person im
                // selben Context laden, damit die Statusänderung mit dem Dok gespeichert wird.
                var person = await db.People.FirstOrDefaultAsync(p => p.Id == personId, cancellationToken);
                if (person is not null)
                {
                    var newDeadUntil = LifeStatusLogic.DeadUntilFrom(input.Timestamp);
                    // Ein bereits laufendes, späteres Tot-Fenster nicht verkürzen (z. B. wenn nachträglich ein
                    // älteres Dok erfasst wird) – nur setzen/verlängern.
                    if (person.DeadUntil is null || newDeadUntil > person.DeadUntil)
                    {
                        person.LifeStatus = LifeStatus.Dead;
                        person.DeadUntil = newDeadUntil;
                    }
                }
                break;
            case MeasureOutcome.Injection:
                // Amnestie-Spritze: Person lebt weiter, verliert aber ihre Erinnerung.
                doc.MemoryDeleted = true;
                break;
        }

        db.PersonDocs.Add(doc);
        // Person + Dok in einem SaveChanges → je ein Audit-Eintrag (Dok „Erstellt", Person „Geaendert").
        await db.SaveChangesAsync(cancellationToken);
        return doc;
    }

    public async Task DeleteAsync(string docId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var doc = await db.PersonDocs.Include(d => d.Person).FirstOrDefaultAsync(d => d.Id == docId, cancellationToken);
        if (doc is null)
        {
            return;
        }
        if (doc.Person?.IsClassified == true && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        // Hat dieses „Erschossen"-Dok das aktuelle Tot-Fenster gesetzt, beim Löschen den Status zurücknehmen
        // (sonst bliebe die versehentlich getötete Person für den Rest des Fensters „Tot").
        if (doc.Person is not null && doc.Outcome == MeasureOutcome.Shot
            && doc.Person.LifeStatus == LifeStatus.Dead
            && doc.Person.DeadUntil == LifeStatusLogic.DeadUntilFrom(doc.Timestamp))
        {
            doc.Person.LifeStatus = LifeStatus.Alive;
            doc.Person.DeadUntil = null;
        }

        var personId = doc.PersonId;
        // Soft-Delete via Interceptor.
        db.PersonDocs.Remove(doc);
        await db.SaveChangesAsync(cancellationToken);
        // Entfernte Maßnahme fällt aus dem Heat (S1) der Fraktionen der Person UND dem Person-Score (P1).
        await threat.NewCalculateForPersonAsync(personId, cancellationToken);
        await threat.NewCalculatePersonScoreAsync(personId, cancellationToken);
    }

    private static string? Empty(string? s) => s.TrimToNull();
}
