using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Tests.Infrastructure;

/// <summary>Factories for the most-referenced entities; every field has a sane default, override via the configure action.</summary>
public static class Seed
{
    public static Agent Agent(string id = "agent-1", Rank rank = Rank.SupervisorySpecialAgent,
        AgentStatus status = AgentStatus.Active, Action<Agent>? configure = null)
    {
        var a = new Agent
        {
            Id = id,
            UserName = id,
            Codename = $"Codename-{id}",
            DiscordId = $"discord-{id}",
            Rank = rank,
            Status = status,
            RegisteredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        configure?.Invoke(a);
        return a;
    }

    public static Person Person(string? id = null, string name = "Max Mustermann",
        Action<Person>? configure = null)
    {
        var p = new Person
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Name = name,
            CaseNumber = "NOOSE-P-2026-0001",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        configure?.Invoke(p);
        return p;
    }

    public static Faction Faction(string? id = null, string name = "Ballas",
        Action<Faction>? configure = null)
    {
        var f = new Faction
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Name = name,
            CaseNumber = "NOOSE-F-2026-0001",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        configure?.Invoke(f);
        return f;
    }

    public static Case Case(string? id = null, string title = "Ermittlung",
        Action<Case>? configure = null)
    {
        var c = new Case
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Title = title,
            CaseNumber = "NOOSE-V-2026-0001",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        configure?.Invoke(c);
        return c;
    }
}
