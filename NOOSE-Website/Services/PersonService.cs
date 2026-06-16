using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.People;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IPersonService" />
public class PersonService(IDbContextFactory<AppDbContext> dbFactory, IFileStorageService fileStorage, IProfileSuggestionService suggestion, ICaseNumberService caseNumber, IThreatScoreService threat) : IPersonService
{
    public async Task<List<Person>> GetListAsync(ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await VisiblePeople(db, scope)
            .Include(p => p.Aliases)
            .OrderByDescending(p => p.ModifiedAt ?? p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Person?> GetDetailAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var person = await db.People
            .Include(p => p.Aliases)
            .Include(p => p.PhoneNumbers)
            .Include(p => p.Vehicles)
            .Include(p => p.Locations)
            .Include(p => p.Weapons)
            .Include(p => p.Photos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (person is null || !await Visibility.IsRecordVisibleAsync(db, nameof(Person), id, scope, cancellationToken))
        {
            return null;
        }
        return person;
    }

    public async Task<List<Person>> GetTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.People.IgnoreQueryFilters()
            .Where(p => p.IsDeleted)
            .OrderByDescending(p => p.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Person>> SearchAsync(string? searchText, bool isLeadership, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.People.Where(p => isLeadership || !p.IsClassified);

        var s = searchText?.Trim();
        if (!string.IsNullOrEmpty(s))
        {
            query = query.Where(p => p.Name.Contains(s) || p.CaseNumber.Contains(s));
        }

        return await query
            .OrderBy(p => p.Name)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Person>> FindDuplicatesAsync(string name, IEnumerable<string> phoneNumbers, bool isLeadership, CancellationToken cancellationToken = default)
    {
        var nameLower = (name ?? string.Empty).Trim().ToLower();
        var numbers = phoneNumbers
            .Select(n => (n ?? string.Empty).Trim())
            .Where(n => n.Length > 0)
            .ToList();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Verschlusssachen nur für die Führung als mögliche Dublette anzeigen – sonst leakt der Warn-Dialog
        // Name + Aktenzeichen klassifizierter Akten an jeden Agenten (Namens-/Nummern-Raten genügt).
        return await db.People
            .Where(p => isLeadership || !p.IsClassified)
            .Include(p => p.PhoneNumbers)
            .Where(p => p.Name.ToLower() == nameLower
                     || p.PhoneNumbers.Any(t => numbers.Contains(t.Number)))
            .ToListAsync(cancellationToken);
    }

    public async Task<Person> CreateAsync(PersonInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        ClassificationHelper.CheckRankGate(input.Classification, actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var person = new Person
        {
            CaseNumber = await caseNumber.NextAsync(db, "P", cancellationToken),
            Name = input.Name.Trim(),
            Description = Empty(input.Description),
            LifeStatus = input.LifeStatus,
            DeadUntil = input.LifeStatus == LifeStatus.Dead ? LifeStatusLogic.DeadUntilFrom(DateTime.UtcNow) : null,
            Classification = input.Classification,
            IsClassified = input.IsClassified,
        };
        ChildrenMap(person, input);
        await SuggestionsStageAsync(db, person, cancellationToken);

        if (input.Classification != Classification.Unknown)
        {
            db.ClassificationHistory.Add(ClassificationHelper.Entry(nameof(Person), person.Id, input.Classification, input.ClassificationJustification, actor));
        }

        db.People.Add(person);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        // Initialen Person-Score berechnen (Einstufung/Waffen/Lebensstatus liegen jetzt committet vor).
        await threat.NewCalculatePersonScoreAsync(person.Id, cancellationToken);
        return person;
    }

    public async Task RefreshAsync(string id, PersonInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var person = await db.People
            .Include(p => p.Aliases)
            .Include(p => p.PhoneNumbers)
            .Include(p => p.Vehicles)
            .Include(p => p.Locations)
            .Include(p => p.Weapons)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Person '{id}' nicht gefunden.");

        // Verschlusssache nur für die Führung bearbeitbar (serverseitig erzwungen, nicht nur via UI).
        if (person.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        var altStatus = person.LifeStatus;
        var altDeadUntil = person.DeadUntil;

        person.Name = input.Name.Trim();
        person.Description = Empty(input.Description);
        person.IsClassified = input.IsClassified;
        person.LifeStatus = input.LifeStatus;
        if (input.LifeStatus == LifeStatus.Dead)
        {
            // Ein frisches 20-Minuten-Fenster startet NUR beim echten Übergang nach „Tot" (vorher kein Tot).
            // War die Person bereits „Tot", bleibt das bestehende Fenster erhalten – auch ein abgelaufenes wird
            // nicht neu gestartet (sonst „tötet" eine harmlose Bearbeitung die Person erneut).
            person.DeadUntil = altStatus != LifeStatus.Dead
                ? LifeStatusLogic.DeadUntilFrom(DateTime.UtcNow)
                : altDeadUntil;
        }
        else
        {
            person.DeadUntil = null;
        }

        // Steckbrief-Kinder vollständig ersetzen (alte hart löschen, neue anlegen).
        db.PersonAliases.RemoveRange(person.Aliases);
        db.PersonPhones.RemoveRange(person.PhoneNumbers);
        db.PersonVehicles.RemoveRange(person.Vehicles);
        db.PersonLocations.RemoveRange(person.Locations);
        db.PersonWeapons.RemoveRange(person.Weapons);
        ChildrenMap(person, input);
        await SuggestionsStageAsync(db, person, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        // Waffen (P2) und Lebensstatus „Flüchtig" (P2) wirken auf den Person-Score → neu berechnen.
        await threat.NewCalculatePersonScoreAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // Löschen/Archivieren ist laut Rechte-Matrix Führung/Admin vorbehalten – serverseitig erzwingen.
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var person = await db.People.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Person '{id}' nicht gefunden.");
        // Hard-Delete wird vom Interceptor in Soft-Delete umgewandelt (+ Audit „Geloescht").
        db.People.Remove(person);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // Wiederherstellen aus dem Papierkorb ist Führung/Admin vorbehalten.
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var person = await db.People.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Person '{id}' nicht gefunden.");

        person.IsDeleted = false;
        person.DeletedAt = null;
        person.DeletedById = null;
        // Interceptor erkennt den Übergang true → false und schreibt „Wiederhergestellt".
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ClassificationSetAsync(string id, Classification @new, string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        ClassificationHelper.CheckRankGate(@new, actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var person = await db.People.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Person '{id}' nicht gefunden.");

        if (person.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        person.Classification = @new;
        db.ClassificationHistory.Add(ClassificationHelper.Entry(nameof(Person), id, @new, justification, actor));
        await db.SaveChangesAsync(cancellationToken);
        // Einstufung bestimmt das Mindest-Band des Person-Scores → neu berechnen.
        await threat.NewCalculatePersonScoreAsync(id, cancellationToken);
    }

    public async Task<List<ClassificationHistory>> GetClassificationHistoryAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // gate invisible records
        if (!await Visibility.IsRecordVisibleAsync(db, nameof(Person), id, scope, cancellationToken))
        {
            return new();
        }
        return await db.ClassificationHistory
            .Where(e => e.EntityType == nameof(Person) && e.EntityId == id)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PersonAffiliation>> GetAffiliationsAsync(string personId, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(db, nameof(Person), personId, scope, cancellationToken))
        {
            return new();
        }
        // partners: hide cross-refs
        if (scope.IsPartner)
        {
            return new();
        }
        var isLeadership = scope.MayClassifiedRead;
        // Join auf die (soft-delete-gefilterten) Eltern-Akten → gelöschte Fraktionen/Gruppen fallen weg.
        // Der globale Soft-Delete-Filter blendet beendete Mitgliedschaften aus → nur aktive (BeendetAm = null).
        var factions = await (
            from m in db.FactionMembers
            where m.PersonId == personId
            join f in db.Factions on m.FactionId equals f.Id
            where isLeadership || !f.IsClassified
            orderby f.Name
            select new PersonAffiliation(nameof(Faction), m.Id, f.Id, f.Name, f.CaseNumber, m.Rank, m.IsLead, m.CreatedAt, (DateTime?)null))
            .ToListAsync(cancellationToken);

        var groups = await (
            from m in db.PersonGroupMembers
            where m.PersonId == personId
            join g in db.PersonGroups on m.PersonGroupId equals g.Id
            where isLeadership || !g.IsClassified
            orderby g.Name
            select new PersonAffiliation(nameof(PersonGroup), m.Id, g.Id, g.Name, g.CaseNumber, m.Role, m.IsLead, m.CreatedAt, (DateTime?)null))
            .ToListAsync(cancellationToken);

        return factions.Concat(groups).ToList();
    }

    public async Task<List<PersonAffiliation>> GetFormerAffiliationsAsync(string personId, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(db, nameof(Person), personId, scope, cancellationToken))
        {
            return new();
        }
        // partners: hide cross-refs
        if (scope.IsPartner)
        {
            return new();
        }
        var isLeadership = scope.MayClassifiedRead;
        // IgnoreQueryFilters: beendete (soft-gelöschte) Mitgliedschaften gezielt holen. Achtung – das schaltet
        // ALLE Filter der Query ab, auch die der Eltern-Akte → Papierkorb/Verschlusssache hier manuell nachsetzen.
        var factions = await (
            from m in db.FactionMembers.IgnoreQueryFilters()
            where m.PersonId == personId && m.IsDeleted
            join f in db.Factions on m.FactionId equals f.Id
            where !f.IsDeleted && (isLeadership || !f.IsClassified)
            select new PersonAffiliation(nameof(Faction), m.Id, f.Id, f.Name, f.CaseNumber, m.Rank, m.IsLead, m.CreatedAt, m.DeletedAt))
            .ToListAsync(cancellationToken);

        var groups = await (
            from m in db.PersonGroupMembers.IgnoreQueryFilters()
            where m.PersonId == personId && m.IsDeleted
            join g in db.PersonGroups on m.PersonGroupId equals g.Id
            where !g.IsDeleted && (isLeadership || !g.IsClassified)
            select new PersonAffiliation(nameof(PersonGroup), m.Id, g.Id, g.Name, g.CaseNumber, m.Role, m.IsLead, m.CreatedAt, m.DeletedAt))
            .ToListAsync(cancellationToken);

        // Neueste Beendigung zuerst (typübergreifend).
        return factions.Concat(groups).OrderByDescending(z => z.EndedAt).ToList();
    }

    public async Task<List<DerivedRelation>> GetDerivedRelationsAsync(string personId, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(db, nameof(Person), personId, scope, cancellationToken))
        {
            return new();
        }
        // partners: hide fan-out
        if (scope.IsPartner)
        {
            return new();
        }
        var isLeadership = scope.MayClassifiedRead;

        // 1. Eigene, für den Betrachter sichtbare Organisationen (Fraktionen + Gruppen + Parteien).
        var myFactions = await db.FactionMembers
            .Where(m => m.PersonId == personId)
            .Join(db.Factions, m => m.FactionId, f => f.Id, (m, f) => f)
            .Where(f => isLeadership || !f.IsClassified)
            .Select(f => new { f.Id, f.Name })
            .ToListAsync(cancellationToken);
        var myGroups = await db.PersonGroupMembers
            .Where(m => m.PersonId == personId)
            .Join(db.PersonGroups, m => m.PersonGroupId, g => g.Id, (m, g) => g)
            .Where(g => isLeadership || !g.IsClassified)
            .Select(g => new { g.Id, g.Name })
            .ToListAsync(cancellationToken);
        var myParties = await db.PartyMembers
            .Where(m => m.PersonId == personId)
            .Join(db.Parties, m => m.PartyId, p => p.Id, (m, p) => p)
            .Where(p => isLeadership || !p.IsClassified)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancellationToken);

        var orgNames = new Dictionary<string, string>();
        foreach (var f in myFactions) orgNames[$"{nameof(Faction)}|{f.Id}"] = f.Name;
        foreach (var g in myGroups) orgNames[$"{nameof(PersonGroup)}|{g.Id}"] = g.Name;
        foreach (var p in myParties) orgNames[$"{nameof(Party)}|{p.Id}"] = p.Name;
        if (orgNames.Count == 0)
        {
            return new();
        }

        // 2. Bündnis-/Konflikt-Verknüpfungen, an denen eine meiner Organisationen beteiligt ist.
        var myOrgIds = myFactions.Select(f => f.Id)
            .Concat(myGroups.Select(g => g.Id))
            .Concat(myParties.Select(p => p.Id))
            .ToList();
        var raw = await db.Links
            .Where(v => (v.Kind == LinkKind.Alliance || v.Kind == LinkKind.Conflict)
                     && (myOrgIds.Contains(v.SourceId) || myOrgIds.Contains(v.TargetId)))
            .Select(v => new { v.SourceType, v.SourceId, v.TargetType, v.TargetId, v.Kind })
            .ToListAsync(cancellationToken);

        // Je Verknüpfung die eigene Seite (Quelle) und die Partner-Organisation bestimmen.
        var partner = new List<(string SourceKey, string PartnerType, string PartnerId, LinkKind Kind)>();
        foreach (var v in raw)
        {
            var sourceKey = $"{v.SourceType}|{v.SourceId}";
            var targetKey = $"{v.TargetType}|{v.TargetId}";
            if (orgNames.ContainsKey(sourceKey))
            {
                partner.Add((sourceKey, v.TargetType, v.TargetId, v.Kind));
            }
            else if (orgNames.ContainsKey(targetKey))
            {
                partner.Add((targetKey, v.SourceType, v.SourceId, v.Kind));
            }
        }
        if (partner.Count == 0)
        {
            return new();
        }

        // 3. Sichtbare Partner-Organisationen auflösen (Name; Verschlusssache nur für Führung).
        var partnerFactionIds = partner.Where(p => p.PartnerType == nameof(Faction)).Select(p => p.PartnerId).Distinct().ToList();
        var partnerGroupsIds = partner.Where(p => p.PartnerType == nameof(PersonGroup)).Select(p => p.PartnerId).Distinct().ToList();
        var partnerPartyIds = partner.Where(p => p.PartnerType == nameof(Party)).Select(p => p.PartnerId).Distinct().ToList();
        var partnerFactions = (await db.Factions
            .Where(f => partnerFactionIds.Contains(f.Id) && (isLeadership || !f.IsClassified))
            .Select(f => new { f.Id, f.Name }).ToListAsync(cancellationToken)).ToDictionary(f => f.Id, f => f.Name);
        var partnerGroups = (await db.PersonGroups
            .Where(g => partnerGroupsIds.Contains(g.Id) && (isLeadership || !g.IsClassified))
            .Select(g => new { g.Id, g.Name }).ToListAsync(cancellationToken)).ToDictionary(g => g.Id, g => g.Name);
        var partnerParties = (await db.Parties
            .Where(p => partnerPartyIds.Contains(p.Id) && (isLeadership || !p.IsClassified))
            .Select(p => new { p.Id, p.Name }).ToListAsync(cancellationToken)).ToDictionary(p => p.Id, p => p.Name);

        // 4. Mitglieder der Partner-Organisationen (Person-Ids je Partner).
        var visibleFactionIds = partnerFactions.Keys.ToList();
        var visibleGroupsIds = partnerGroups.Keys.ToList();
        var visiblePartyIds = partnerParties.Keys.ToList();
        var factionMembers = await db.FactionMembers
            .Where(m => visibleFactionIds.Contains(m.FactionId))
            .Select(m => new { m.FactionId, m.PersonId }).ToListAsync(cancellationToken);
        var groupsMembers = await db.PersonGroupMembers
            .Where(m => visibleGroupsIds.Contains(m.PersonGroupId))
            .Select(m => new { m.PersonGroupId, m.PersonId }).ToListAsync(cancellationToken);
        var partyMembers = await db.PartyMembers
            .Where(m => visiblePartyIds.Contains(m.PartyId))
            .Select(m => new { m.PartyId, m.PersonId }).ToListAsync(cancellationToken);
        var membersPerPartner = factionMembers.GroupBy(m => m.FactionId).ToDictionary(g => g.Key, g => g.Select(x => x.PersonId).ToList());
        foreach (var grp in groupsMembers.GroupBy(m => m.PersonGroupId))
        {
            membersPerPartner[grp.Key] = grp.Select(x => x.PersonId).ToList();
        }
        foreach (var grp in partyMembers.GroupBy(m => m.PartyId))
        {
            membersPerPartner[grp.Key] = grp.Select(x => x.PersonId).ToList();
        }

        // 5. Kandidaten bilden (dedupliziert je (Person, Art); sich selbst ausschließen).
        var candidates = new Dictionary<(string PersonId, LinkKind Kind), (string SourceName, string PartnerName)>();
        foreach (var p in partner)
        {
            var partnerName = p.PartnerType switch
            {
                nameof(Faction) => partnerFactions.TryGetValue(p.PartnerId, out var fn) ? fn : null,
                nameof(PersonGroup) => partnerGroups.TryGetValue(p.PartnerId, out var gn) ? gn : null,
                nameof(Party) => partnerParties.TryGetValue(p.PartnerId, out var pn) ? pn : null,
                _ => null,
            };
            if (partnerName is null || !membersPerPartner.TryGetValue(p.PartnerId, out var mids))
            {
                continue;
            }
            var sourceName = orgNames[p.SourceKey];
            foreach (var mid in mids)
            {
                if (mid == personId)
                {
                    continue;
                }
                candidates.TryAdd((mid, p.Kind), (sourceName, partnerName));
            }
        }
        if (candidates.Count == 0)
        {
            return new();
        }

        // 6. Personen auflösen (Name/Aktenzeichen; Verschlusssache nur für Führung).
        var personIds = candidates.Keys.Select(k => k.PersonId).Distinct().ToList();
        var people = (await db.People
            .Where(p => personIds.Contains(p.Id) && (isLeadership || !p.IsClassified))
            .Select(p => new { p.Id, p.Name, p.CaseNumber }).ToListAsync(cancellationToken))
            .ToDictionary(p => p.Id);

        var result = new List<DerivedRelation>();
        foreach (var ((pid, kind), (sourceName, partnerName)) in candidates)
        {
            if (!people.TryGetValue(pid, out var person))
            {
                continue;
            }
            result.Add(new DerivedRelation(kind, person.Id, person.Name, person.CaseNumber, sourceName, partnerName));
        }
        return result
            .OrderBy(e => e.Kind)
            .ThenBy(e => e.PersonName)
            .ToList();
    }

    public async Task<PersonPhoto> PhotoAddAsync(string personId, Stream content, string originalName, string contentType, long size, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        if (!fileStorage.IsAllowedType(contentType))
        {
            throw new InvalidOperationException($"Dateityp '{contentType}' ist nicht erlaubt.");
        }
        // Größenlimit serverseitig erzwingen (nicht nur in der UI) – verhindert Disk-Filling über andere Pfade.
        if (size > fileStorage.MaxBytes)
        {
            throw new InvalidOperationException($"Datei zu groß (max. {fileStorage.MaxBytes / (1024 * 1024)} MB).");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Existenz + Verschlusssache-Sichtbarkeit der Akte prüfen, BEVOR eine Datei geschrieben wird.
        var person = await db.People.FirstOrDefaultAsync(p => p.Id == personId, cancellationToken)
            ?? throw new InvalidOperationException($"Person '{personId}' nicht gefunden.");
        if (person.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        var fileName = await fileStorage.SaveAsync(content, contentType, cancellationToken);
        var photo = new PersonPhoto
        {
            PersonId = personId,
            FileNameSaved = fileName,
            OriginalName = originalName,
            ContentType = contentType,
            SizeBytes = size,
            CreatedAt = DateTime.UtcNow,
            CreatedById = actor.GetAgentId(),
        };
        db.PersonPhotos.Add(photo);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Schlägt der DB-Insert fehl, die bereits geschriebene Datei wieder entfernen (kein verwaister Anhang).
            fileStorage.Delete(fileName);
            throw;
        }
        return photo;
    }

    public async Task PhotoRemoveAsync(string photoId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var photo = await db.PersonPhotos.Include(f => f.Person).FirstOrDefaultAsync(f => f.Id == photoId, cancellationToken);
        if (photo is null)
        {
            return;
        }
        if (photo.Person?.IsClassified == true && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        // Erst den DB-Datensatz entfernen (Quelle der Wahrheit), dann die Datei löschen. So bleibt
        // bei einem Speicherfehler kein verwaister Datensatz zurück, der auf eine fehlende Datei zeigt.
        db.PersonPhotos.Remove(photo);
        await db.SaveChangesAsync(cancellationToken);
        fileStorage.Delete(photo.FileNameSaved);
    }

    public async Task<PersonPhoto?> GetPhotoWithPersonAsync(string photoId, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var photo = await db.PersonPhotos.Include(f => f.Person).FirstOrDefaultAsync(f => f.Id == photoId, cancellationToken);
        if (photo?.Person is null)
        {
            return null;
        }
        if (scope.PartnerAgency is { } agency)
        {
            // partners: parent visible AND (whole-record or this photo released)
            return await PartnerVisibility.IsChildVisibleToPartnerAsync(db, nameof(Person), photo.PersonId, nameof(PersonPhoto), photoId, agency, cancellationToken)
                ? photo
                : null;
        }
        return photo.Person.IsClassified && !scope.MayClassifiedRead ? null : photo;
    }

    public async Task<List<AuditLog>> GetHistoryAsync(string personId, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(db, nameof(Person), personId, scope, cancellationToken))
        {
            return new();
        }
        // partners: hide audit
        if (scope.IsPartner)
        {
            return new();
        }
        // Kind-IDs (Doks) einsammeln, damit deren Audit-Einträge in der Akten-Historie erscheinen.
        var docIds = await db.PersonDocs.IgnoreQueryFilters()
            .Where(d => d.PersonId == personId)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        var ids = new HashSet<string>(docIds) { personId };
        var types = new[] { nameof(Person), nameof(PersonDoc) };

        return await db.AuditLogs
            .Where(a => types.Contains(a.EntityType) && ids.Contains(a.EntityId))
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    // ---- Helfer ----

    private static void ChildrenMap(Person person, PersonInput input)
    {
        person.Aliases = input.Aliases
            .Where(a => !string.IsNullOrWhiteSpace(a.AliasName))
            .Select(a => new PersonAlias { PersonId = person.Id, AliasName = a.AliasName.Trim() })
            .ToList();
        person.PhoneNumbers = input.PhoneNumbers
            .Where(t => !string.IsNullOrWhiteSpace(t.Number))
            .Select(t => new PersonPhone { PersonId = person.Id, Number = t.Number.Trim(), Designation = Empty(t.Designation) })
            .ToList();
        person.Vehicles = input.Vehicles
            .Where(f => !string.IsNullOrWhiteSpace(f.Designation) || !string.IsNullOrWhiteSpace(f.LicensePlate))
            .Select(f => new PersonVehicle { PersonId = person.Id, Designation = (f.Designation ?? string.Empty).Trim(), LicensePlate = Empty(f.LicensePlate) })
            .ToList();
        person.Locations = input.Locations
            .Where(o => !string.IsNullOrWhiteSpace(o.Text))
            .Select(o => new PersonLocation { PersonId = person.Id, Text = o.Text.Trim(), Note = Empty(o.Note) })
            .ToList();
        person.Weapons = input.Weapons
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .Select(w => new PersonWeapon { PersonId = person.Id, Text = w.Text.Trim() })
            .ToList();
    }

    /// <summary>
    /// Speist die erfassten Steckbrief-Werte in den gemeinsamen Vorschlagskatalog ein (Waffen/Fahrzeuge/Orte).
    /// Verschlusssachen bleiben außen vor, damit klassifizierte Werte nicht in die geteilte Liste gelangen.
    /// Merkt nur im übergebenen Context vor – persistiert wird mit dem nachfolgenden SaveChanges der Person (atomar).
    /// </summary>
    private async Task SuggestionsStageAsync(AppDbContext db, Person person, CancellationToken cancellationToken)
    {
        if (person.IsClassified)
        {
            return;
        }
        await suggestion.StageAsync(db, SuggestionType.Weapon, person.Weapons.Select(w => w.Text), cancellationToken);
        await suggestion.StageAsync(db, SuggestionType.Vehicle, person.Vehicles.Select(f => f.Designation), cancellationToken);
        await suggestion.StageAsync(db, SuggestionType.Location, person.Locations.Select(o => o.Text), cancellationToken);
    }

    private static string? Empty(string? s) => s.TrimToNull();

    // scope-filtered people query
    private static IQueryable<Person> VisiblePeople(AppDbContext db, ViewerScope scope)
        => scope.PartnerAgency is { } agency
            ? db.People.OnlyPartnerVisible(db, agency)
            : db.People.Where(p => scope.MayClassifiedRead || !p.IsClassified);
}
