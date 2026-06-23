using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Seeds the demo agent and example records; idempotent (skips by name) and admin-gated.</summary>
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
        // case numbers need an enclosing transaction (see CaseNumberService)
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        added += await SeedFactionsAsync(db, cancellationToken);
        added += await SeedPeopleAsync(db, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return added;
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
        await userManager.CreateAsync(agent);
        return 1;
    }

    private async Task<int> SeedFactionsAsync(AppDbContext db, CancellationToken ct)
    {
        var have = new HashSet<string>(
            await db.Factions.IgnoreQueryFilters().Select(f => f.Name).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var spec in FactionSpecs)
        {
            if (have.Contains(spec.Name))
            {
                continue;
            }
            db.Factions.Add(new Faction
            {
                CaseNumber = await caseNumbers.NextAsync(db, "F", ct),
                Name = spec.Name,
                Kind = spec.Kind,
                Description = spec.Description,
                RecognitionColor = spec.Color,
                Classification = spec.Classification,
                ThreatScore = spec.ThreatScore,
                ThreatConfidence = 70,
                ScoreCalculatedAt = DateTime.UtcNow,
                CreatedById = DemoIdentity.AgentId,
            });
            added++;
        }
        return added;
    }

    private async Task<int> SeedPeopleAsync(AppDbContext db, CancellationToken ct)
    {
        var have = new HashSet<string>(
            await db.People.IgnoreQueryFilters().Select(p => p.Name).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var spec in PeopleSpecs)
        {
            if (have.Contains(spec.Name))
            {
                continue;
            }
            db.People.Add(new Person
            {
                CaseNumber = await caseNumbers.NextAsync(db, "P", ct),
                Name = spec.Name,
                Description = spec.Description,
                Classification = spec.Classification,
                ThreatScore = spec.ThreatScore,
                ThreatConfidence = 65,
                ScoreCalculatedAt = DateTime.UtcNow,
                CreatedById = DemoIdentity.AgentId,
            });
            added++;
        }
        return added;
    }

    private static readonly (string Name, string Kind, string Description, string Color, Classification Classification, int ThreatScore)[] FactionSpecs =
    [
        ("Vagos Demo-Clan", "Streetgang", "Beispiel-Fraktion für die Demo-Instanz.", "#F9A825", Classification.SuspicionCase, 58),
        ("Marabunta Demo", "Streetgang", "Beispiel-Fraktion für die Demo-Instanz.", "#2E7D32", Classification.ReviewCase, 34),
        ("Demo-Syndikat", "Organisierte Kriminalität", "Beispiel-Fraktion mit hoher Einstufung.", "#C62828", Classification.SecuredStateThreatening, 82),
    ];

    private static readonly (string Name, string Description, Classification Classification, int ThreatScore)[] PeopleSpecs =
    [
        ("Max Demo", "Beispiel-Person für die Demo-Instanz.", Classification.ReviewCase, 22),
        ("Erika Beispiel", "Beispiel-Person mit Verdachtsfall.", Classification.SuspicionCase, 49),
        ("John Showcase", "Beispiel-Person, gesichert staatsgefährdend.", Classification.SecuredStateThreatening, 88),
        ("Lara Muster", "Beispiel-Person für die Demo-Instanz.", Classification.Unknown, 5),
    ];
}
