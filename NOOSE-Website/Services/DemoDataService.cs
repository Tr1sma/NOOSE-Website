using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Seeds the demo agent and a rich, interconnected example dataset; idempotent (skips existing by natural key) and admin-gated. Never auto-runs at startup.</summary>
public class DemoDataService(
    IDbContextFactory<AppDbContext> dbFactory,
    ICaseNumberService caseNumbers,
    UserManager<Agent> userManager) : IDemoDataService
{
    public async Task<int> SeedAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireAdmin(actor);

        var added = await EnsureDemoAgentAsync();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // case numbers need an enclosing transaction (see CaseNumberService); client-generated GUID
        // PKs let us wire relations/members in-memory without an intermediate save.
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var (factions, fAdded) = await SeedFactionsAsync(db, cancellationToken);
        var (people, pAdded, newPeople) = await SeedPeopleAsync(db, cancellationToken);
        var mAdded = await SeedMembersAsync(db, people, factions, cancellationToken);
        var rAdded = await SeedRelationsAsync(db, people, cancellationToken);
        var lAdded = await SeedFactionLinksAsync(db, factions, cancellationToken);
        var dAdded = SeedDocs(db, people, newPeople);
        var oAdded = SeedObservations(db, people, newPeople);

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return added + fAdded + pAdded + mAdded + rAdded + lAdded + dAdded + oAdded;
    }

    private async Task<int> EnsureDemoAgentAsync()
    {
        if (await userManager.FindByIdAsync(DemoIdentity.AgentId) is not null)
        {
            return 0;
        }
        var agent = new Agent
        {
            Id = DemoIdentity.AgentId,
            UserName = "demo-agent",
            Codename = DemoIdentity.Codename,
            DiscordId = "demo",
            Status = AgentStatus.Active,
            Rank = Rank.Director,
            IsTRU = true,
            IsHRB = true,
            RegisteredAt = DateTime.UtcNow,
        };
        var result = await userManager.CreateAsync(agent);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Demo-Agent konnte nicht angelegt werden: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }
        return 1;
    }

    private async Task<(Dictionary<string, Faction> Map, int Added)> SeedFactionsAsync(AppDbContext db, CancellationToken ct)
    {
        var map = (await db.Factions.IgnoreQueryFilters().ToListAsync(ct))
            .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var spec in DemoDataSeed.Factions)
        {
            if (map.ContainsKey(spec.Name))
            {
                continue;
            }
            var faction = new Faction
            {
                CaseNumber = await caseNumbers.NextAsync(db, "F", ct),
                Name = spec.Name,
                Kind = spec.Kind,
                RecognitionColor = spec.Color,
                Classification = spec.Classification,
                IsClassified = spec.Classified,
                IsStateFaction = spec.StateFaction,
                ThreatScore = spec.ThreatScore,
                ThreatConfidence = spec.ThreatScore is null ? null : 72,
                ScoreCalculatedAt = DateTime.UtcNow,
                Estate = spec.Estate,
                Targets = spec.Targets,
                Description = spec.Description,
                Radio = spec.Radio,
                EstimatedMemberCount = spec.EstimatedMembers,
                CreatedById = DemoIdentity.AgentId,
            };
            // higher ranks first
            var order = spec.Ranks.Length;
            foreach (var rank in spec.Ranks)
            {
                faction.Ranks.Add(new FactionRank { Designation = rank, Order = order-- });
            }
            foreach (var a in spec.Activities)
            {
                faction.Activities.Add(new FactionActivity
                {
                    Title = a.Title,
                    Kind = a.Kind,
                    Timestamp = DateTime.UtcNow.AddDays(-a.DaysAgo),
                    Location = a.Location,
                    Description = a.Description,
                    CreatedById = DemoIdentity.AgentId,
                });
            }
            db.Factions.Add(faction);
            map[spec.Name] = faction;
            added++;
        }
        return (map, added);
    }

    private async Task<(Dictionary<string, Person> Map, int Added, HashSet<string> NewNames)> SeedPeopleAsync(AppDbContext db, CancellationToken ct)
    {
        var map = (await db.People.IgnoreQueryFilters().ToListAsync(ct))
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var added = 0;
        var newNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in DemoDataSeed.People)
        {
            if (map.ContainsKey(spec.Name))
            {
                continue;
            }
            var person = new Person
            {
                CaseNumber = await caseNumbers.NextAsync(db, "P", ct),
                Name = spec.Name,
                Description = spec.Description,
                Classification = spec.Classification,
                IsClassified = spec.Classified,
                LifeStatus = spec.LifeStatus,
                ThreatScore = spec.ThreatScore,
                ThreatConfidence = spec.ThreatScore is null ? null : 68,
                ScoreCalculatedAt = DateTime.UtcNow,
                CreatedById = DemoIdentity.AgentId,
            };
            foreach (var alias in spec.Aliases)
            {
                person.Aliases.Add(new PersonAlias { AliasName = alias });
            }
            foreach (var (number, label) in spec.Phones)
            {
                person.PhoneNumbers.Add(new PersonPhone { Number = number, Designation = label });
            }
            foreach (var (designation, plate) in spec.Vehicles)
            {
                person.Vehicles.Add(new PersonVehicle { Designation = designation, LicensePlate = plate });
            }
            foreach (var weapon in spec.Weapons)
            {
                person.Weapons.Add(new PersonWeapon { Text = weapon });
            }
            foreach (var (place, note) in spec.Locations)
            {
                person.Locations.Add(new PersonLocation { Text = place, Note = note });
            }
            db.People.Add(person);
            map[spec.Name] = person;
            newNames.Add(spec.Name);
            added++;
        }
        return (map, added, newNames);
    }

    private static async Task<int> SeedMembersAsync(
        AppDbContext db, Dictionary<string, Person> people, Dictionary<string, Faction> factions, CancellationToken ct)
    {
        var existing = new HashSet<string>(
            await db.FactionMembers.IgnoreQueryFilters().Select(m => m.PersonId + "|" + m.FactionId).ToListAsync(ct));

        var added = 0;
        foreach (var spec in DemoDataSeed.Members)
        {
            if (!people.TryGetValue(spec.Person, out var person) || !factions.TryGetValue(spec.Faction, out var faction))
            {
                continue;
            }
            if (!existing.Add(person.Id + "|" + faction.Id))
            {
                continue;
            }
            db.FactionMembers.Add(new FactionMember
            {
                PersonId = person.Id,
                FactionId = faction.Id,
                Rank = spec.Rank,
                IsLead = spec.IsLead,
                CreatedById = DemoIdentity.AgentId,
            });
            added++;
        }
        return added;
    }

    private static async Task<int> SeedRelationsAsync(AppDbContext db, Dictionary<string, Person> people, CancellationToken ct)
    {
        var rows = await db.PersonRelations.IgnoreQueryFilters()
            .Select(r => new { r.PersonAId, r.PersonBId, r.Type }).ToListAsync(ct);
        var existing = new HashSet<string>(rows.Select(r => RelationKey(r.PersonAId, r.PersonBId, r.Type)));

        var added = 0;
        foreach (var spec in DemoDataSeed.Relations)
        {
            if (!people.TryGetValue(spec.A, out var a) || !people.TryGetValue(spec.B, out var b))
            {
                continue;
            }
            if (!existing.Add(RelationKey(a.Id, b.Id, spec.Type)))
            {
                continue;
            }
            db.PersonRelations.Add(new PersonRelation
            {
                PersonAId = a.Id,
                PersonBId = b.Id,
                Type = spec.Type,
                Note = spec.Note,
                CreatedById = DemoIdentity.AgentId,
            });
            added++;
        }
        return added;
    }

    private static async Task<int> SeedFactionLinksAsync(AppDbContext db, Dictionary<string, Faction> factions, CancellationToken ct)
    {
        var rows = await db.Links.IgnoreQueryFilters()
            .Where(l => l.SourceType == nameof(Faction) && l.TargetType == nameof(Faction))
            .Select(l => new { l.SourceId, l.TargetId }).ToListAsync(ct);
        var existing = new HashSet<string>(rows.Select(r => PairKey(r.SourceId, r.TargetId)));

        var added = 0;
        foreach (var spec in DemoDataSeed.FactionLinks)
        {
            if (!factions.TryGetValue(spec.Source, out var s) || !factions.TryGetValue(spec.Target, out var t))
            {
                continue;
            }
            if (!existing.Add(PairKey(s.Id, t.Id)))
            {
                continue;
            }
            db.Links.Add(new Link
            {
                SourceType = nameof(Faction),
                SourceId = s.Id,
                TargetType = nameof(Faction),
                TargetId = t.Id,
                Kind = spec.Kind,
                Label = spec.Label,
                CreatedById = DemoIdentity.AgentId,
            });
            added++;
        }
        return added;
    }

    // docs/observations have no natural key; only seed them for freshly created people so re-runs don't duplicate
    private static int SeedDocs(AppDbContext db, Dictionary<string, Person> people, HashSet<string> newNames)
    {
        var added = 0;
        foreach (var spec in DemoDataSeed.Docs)
        {
            if (!newNames.Contains(spec.Person) || !people.TryGetValue(spec.Person, out var person))
            {
                continue;
            }
            db.PersonDocs.Add(new PersonDoc
            {
                PersonId = person.Id,
                Timestamp = DateTime.UtcNow.AddDays(-spec.DaysAgo),
                Reason = spec.Reason,
                Faction = spec.Faction,
                Outcome = spec.Outcome,
                TruthSerum = spec.TruthSerum,
                ReceivedInformation = spec.Info,
                CreatedById = DemoIdentity.AgentId,
            });
            added++;
        }
        return added;
    }

    private static int SeedObservations(AppDbContext db, Dictionary<string, Person> people, HashSet<string> newNames)
    {
        var added = 0;
        foreach (var spec in DemoDataSeed.Observations)
        {
            if (!newNames.Contains(spec.Person) || !people.TryGetValue(spec.Person, out var person))
            {
                continue;
            }
            var start = DateTime.UtcNow.AddDays(-spec.StartDaysAgo);
            db.Observations.Add(new Observation
            {
                PersonId = person.Id,
                Start = start,
                End = spec.DurationHours is int hours ? start.AddHours(hours) : null,
                Location = spec.Location,
                Sighting = spec.Sighting,
                Result = spec.Result,
                CreatedById = DemoIdentity.AgentId,
            });
            added++;
        }
        return added;
    }

    private static string RelationKey(string a, string b, RelationType type)
    {
        var (x, y) = string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
        return $"{x}|{y}|{(int)type}";
    }

    private static string PairKey(string a, string b)
        => string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
}
