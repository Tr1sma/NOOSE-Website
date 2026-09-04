using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Kasse;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Services;

/// <summary>Demo data for the public area: modules, wanted notices, bounties, tips, tickets, objections and the editorial content.</summary>
public partial class DemoDataService
{
    private const string WantedPrefix = "FA";
    private const string TipPrefix = "H";
    private const string TicketPrefix = "T";
    private const string ObjectionPrefix = "EIN";
    private const string ReceiptPrefix = "BEL";
    private const string KassePrefix = "KAS";

    /// <summary>Citizen accounts backing the tips, tickets and objections. Id doubles as UserName and DiscordId.</summary>
    private static readonly CitizenSpec[] Citizens =
    [
        new("demo-buerger-vega", "marisol.vega", "Marisol", "Vega", 6, 190, false, null),
        new("demo-buerger-okonkwo", "d.okonkwo", "Daniel", "Okonkwo", 2, 120, false, null),
        new("demo-buerger-hartmann", "lena.hartmann", "Lena", "Hartmann", 0, 41, false, null),
        new("demo-buerger-cruz", "tomas.cruz", "Tomás", "Cruz", 9, 260, false, null),
        new("demo-buerger-navarro", "a.navarro", "Alessio", "Navarro", 1, 77, false, null),
        new("demo-buerger-baker", "priya.baker", "Priya", "Baker", 0, 33, true,
            "Wiederholt unbrauchbare Meldungen ohne Ortsangabe; nach zwei Verwarnungen für Einreichungen gesperrt."),
    ];

    private sealed record CitizenSpec(
        string Id, string DiscordName, string FirstName, string LastName,
        int ConfirmedTips, int RegisteredDaysAgo, bool Blocked, string? BlockReason);

    /// <summary>Creates the citizen accounts through Identity so name normalisation and security stamps are set. Runs outside the seeding transaction.</summary>
    private async Task<int> EnsureCitizenAccountsAsync()
    {
        var added = 0;
        foreach (var spec in Citizens)
        {
            if (await userManager.FindByIdAsync(spec.Id) is not null)
            {
                continue;
            }
            var citizen = new Agent
            {
                Id = spec.Id,
                UserName = spec.Id,
                DiscordId = spec.Id,
                DiscordUsername = spec.DiscordName,
                Codename = string.Empty,
                Status = AgentStatus.Civilian,
                Rank = null,
                RegisteredAt = DateTime.UtcNow.AddDays(-spec.RegisteredDaysAgo),
            };
            var result = await userManager.CreateAsync(citizen);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Demo-Bürgerkonto {spec.Id} konnte nicht angelegt werden: "
                    + string.Join("; ", result.Errors.Select(e => e.Description)));
            }
            added++;
        }
        return added;
    }

    /// <summary>Turns on every public module and clears the kill switch so the demo shows the outward area at all.</summary>
    private static async Task<int> SeedPublicModulesAsync(AppDbContext db, CancellationToken ct)
    {
        var changed = 0;
        var keys = PublicModules.All.Select(m => m.Key).ToArray();
        var rows = await db.OeffentlicheModule.Where(m => keys.Contains(m.Key)).ToListAsync(ct);
        var known = rows.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var row in rows.Where(r => !r.IsEnabled))
        {
            row.IsEnabled = true;
            changed++;
        }
        // the module seeder runs first, but a key added after this database was created would still be missing
        foreach (var definition in PublicModules.All.Where(d => !known.Contains(d.Key)))
        {
            db.OeffentlicheModule.Add(new OeffentlichesModul
            {
                Key = definition.Key,
                IsEnabled = true,
                SortOrder = definition.SortOrder,
            });
            changed++;
        }

        changed += await UpsertSettingAsync(db, SystemSettingKeys.PublicAreaKillSwitch, "false", ct);
        return changed;
    }

    /// <summary>Sets the hazard level through its own service — it owns those settings rows and their cache.</summary>
    /// <remarks>Twice, so the page has a previous level to show as a trend; the service derives date and trend itself.</remarks>
    private async Task<int> SeedSituationLevelAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        if (await situation.GetForEditAsync(actor, ct) is not null)
        {
            return 0;
        }
        await situation.SetAsync(new PublicSituationInput
        {
            Level = PublicSituationLevel.Niedrig,
            Note = "Keine besonderen Vorkommnisse.",
        }, actor, ct);
        await situation.SetAsync(new PublicSituationInput
        {
            Level = PublicSituationLevel.Erhoeht,
            Note = "Nach mehreren Auseinandersetzungen zwischen rivalisierenden Gruppierungen im Süden der Stadt "
                + "gilt bis auf Weiteres erhöhte Wachsamkeit. Meiden Sie nächtliche Aufenthalte in Davis und Rancho.",
        }, actor, ct);
        return 2;
    }

    private static async Task<int> UpsertSettingAsync(AppDbContext db, string key, string value, CancellationToken ct)
    {
        var row = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null)
        {
            db.SystemSettings.Add(new SystemSetting { Key = key, Value = value });
            return 1;
        }
        if (string.Equals(row.Value, value, StringComparison.Ordinal))
        {
            return 0;
        }
        row.Value = value;
        return 1;
    }

    // ---- wanted notices ----------------------------------------------------

    private sealed record WantedSpec(
        PublicWantedKind Kind, PublicWantedStatus Status, string Charge, string? Aliases,
        string? LastArea, string? VehicleText, HazardLevel Hazard, int PublishedDaysAgo,
        int ViewCount, string[] Hints, decimal Bounty, bool BountyIsCap, int? CapturedDaysAgo);

    private static readonly WantedSpec[] WantedSpecs =
    [
        new(PublicWantedKind.Fahndung, PublicWantedStatus.Veroeffentlicht,
            "Dringender Tatverdacht des schweren Raubes in drei Fällen, des unerlaubten Handels mit Kriegswaffen "
            + "sowie der gefährlichen Körperverletzung. Die Person gilt als bewaffnet.",
            "„El Jefe\", „Der Alte\"", "Rancho, East Los Santos", "Dunkler SUV, Kennzeichen wechselnd",
            HazardLevel.Critical, 16, 1483, ["bewaffnet", "gewaltbereit", "nicht selbst eingreifen"], 250_000m, false, null),
        new(PublicWantedKind.Fahndung, PublicWantedStatus.Veroeffentlicht,
            "Verdacht der Beteiligung an einer kriminellen Vereinigung, des bandenmäßigen Betäubungsmittelhandels "
            + "und des Widerstands gegen Vollstreckungsbeamte.",
            "„Ghost\"", "Davis, Umgebung Grove Street", null,
            HazardLevel.High, 12, 902, ["bewaffnet", "flieht mit Fahrzeug"], 120_000m, false, null),
        new(PublicWantedKind.Fahndung, PublicWantedStatus.Veroeffentlicht,
            "Dringender Tatverdacht der Geldwäsche in besonders schwerem Fall und der Bestechung von Amtsträgern. "
            + "Hinweise auf Fluchtvorbereitungen liegen vor.",
            null, "Vinewood Hills", "Heller Sportwagen", HazardLevel.High, 9, 671,
            ["flieht mit Fahrzeug"], 90_000m, true, null),
        new(PublicWantedKind.Fahndung, PublicWantedStatus.Veroeffentlicht,
            "Verdacht des Menschenhandels und der Freiheitsberaubung. Zeugen werden gebeten, sich zu melden.",
            "„Doc\"", "Hafenviertel, Elysian Island", null, HazardLevel.Medium, 6, 388,
            ["nicht selbst eingreifen"], 45_000m, false, null),
        new(PublicWantedKind.Fahndung, PublicWantedStatus.Veroeffentlicht,
            "Verdacht der schweren Brandstiftung sowie der Sachbeschädigung in 14 Fällen.",
            null, "Sandy Shores", null, HazardLevel.Medium, 4, 205, [], 0m, false, null),
        new(PublicWantedKind.Fahndung, PublicWantedStatus.Veroeffentlicht,
            "Verdacht des gewerbsmäßigen Betrugs zum Nachteil älterer Personen.",
            "„Sunny\"", "Del Perro, Strandpromenade", null, HazardLevel.Low, 2, 96, [], 15_000m, false, null),
        new(PublicWantedKind.Fahndung, PublicWantedStatus.Gefasst,
            "Dringender Tatverdacht des schweren Bandendiebstahls. Die Person wurde inzwischen gestellt.",
            null, "Mirror Park", null, HazardLevel.High, 34, 2140, ["bewaffnet"], 60_000m, false, 5),
        new(PublicWantedKind.Fahndung, PublicWantedStatus.Gefasst,
            "Verdacht der Erpressung und der Nötigung. Die Ausschreibung ist abgeschlossen.",
            null, "Little Seoul", null, HazardLevel.Medium, 51, 1176, [], 0m, false, 19),
        new(PublicWantedKind.Fahndung, PublicWantedStatus.Zurueckgezogen,
            "Die Ausschreibung wurde zurückgezogen.", null, "La Mesa", null, HazardLevel.Low, 28, 314, [], 0m, false, null),
        new(PublicWantedKind.Fahrzeug, PublicWantedStatus.Veroeffentlicht,
            "Das Fahrzeug wurde bei einem Überfall auf einen Geldtransporter eingesetzt und wird zur Sicherstellung "
            + "ausgeschrieben. Der Halter ist unbekannt.",
            null, "zuletzt gesehen: Route 68, Höhe Harmony", "Schwarzer Pick-up, Kennzeichen 45HGF221",
            HazardLevel.Medium, 7, 431, ["nicht selbst eingreifen"], 20_000m, false, null),
        new(PublicWantedKind.Waffe, PublicWantedStatus.Veroeffentlicht,
            "Bei einer Durchsuchung wurde festgestellt, dass eine registrierte Dienstwaffe entwendet wurde. "
            + "Zweckdienliche Hinweise nimmt jede Dienststelle entgegen.",
            null, null, null, HazardLevel.Low, 3, 158, [], 0m, false, null),
    ];

    private async Task<PublicSeedResult> SeedWantedAsync(
        AppDbContext db, Dictionary<string, Person> people, Dictionary<string, Faction> factions,
        List<TimeStamp> stamps, CancellationToken ct)
    {
        var result = new PublicSeedResult();

        // an existing notice keeps its person; the natural key is the person plus the kind
        var existing = await db.OeffentlicheFahndungen.IgnoreQueryFilters()
            .Select(w => new { w.PersonId, w.Kind }).ToListAsync(ct);
        var taken = existing.Select(e => e.PersonId + "|" + (int)e.Kind).ToHashSet(StringComparer.Ordinal);

        // the suppression belt drops a notice whose person is classified, so such a row would seed data nobody sees
        var candidates = people.Values
            .Where(p => !p.IsClassified && !p.IsTRUClassified && !p.IsHRBClassified && !p.IsDeleted)
            .OrderByDescending(p => p.ThreatScore ?? 0)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();
        if (candidates.Count == 0)
        {
            return result;
        }

        var hints = await EnsureHintsAsync(db, ct);
        var index = 0;
        foreach (var spec in WantedSpecs)
        {
            if (index >= candidates.Count)
            {
                break;
            }
            var person = candidates[index++];
            if (!taken.Add(person.Id + "|" + (int)spec.Kind))
            {
                continue;
            }

            var published = DateTime.UtcNow.AddDays(-spec.PublishedDaysAgo);
            var notice = new OeffentlicheFahndung
            {
                CaseNumber = await caseNumbers.NextAsync(db, WantedPrefix, ct),
                Kind = spec.Kind,
                Status = spec.Status,
                PersonId = person.Id,
                DisplayName = spec.Kind == PublicWantedKind.Fahrzeug ? "Schwarzer Pick-up 45HGF221"
                    : spec.Kind == PublicWantedKind.Waffe ? "Entwendete Dienstwaffe (Combat Pistol)"
                    : person.Name,
                AliasText = spec.Aliases,
                ChargeHtml = Paragraph(spec.Charge),
                LastArea = spec.LastArea,
                VehicleText = spec.VehicleText,
                PublicHazardLevel = spec.Hazard,
                HazardLevelIsManual = true,
                ExpiresAt = null,
                BountyIsCap = spec.BountyIsCap,
                PublishedAt = published,
                PublishedById = DemoIdentity.AgentId,
                ViewCount = spec.ViewCount,
                CapturedAt = spec.CapturedDaysAgo is int captured ? DateTime.UtcNow.AddDays(-captured) : null,
                RetractedAt = spec.Status == PublicWantedStatus.Zurueckgezogen ? DateTime.UtcNow.AddDays(-3) : null,
                RetractedReason = spec.Status == PublicWantedStatus.Zurueckgezogen
                    ? "Der Tatverdacht hat sich nach Auswertung der Videoaufzeichnungen nicht erhärtet."
                    : null,
            };
            db.OeffentlicheFahndungen.Add(notice);
            stamps.Add(new TimeStamp(nameof(OeffentlicheFahndung), notice.Id, published));
            result.Added++;

            foreach (var hintName in spec.Hints)
            {
                if (hints.TryGetValue(hintName, out var hint))
                {
                    db.FahndungWarnhinweise.Add(new FahndungWarnhinweis
                    {
                        FahndungId = notice.Id,
                        WarnhinweisId = hint.Id,
                    });
                    result.Added++;
                }
            }

            var seeded = new SeededNotice(notice, person, spec.Bounty, spec.Hazard, spec.Status);
            if (spec.Bounty > 0m)
            {
                result.Added += await SeedBountyAsync(db, notice, spec.Bounty, published, seeded.Shares, ct);
            }

            // one faction link so the internal side of the notice is not empty
            var faction = factions.Values.FirstOrDefault(f => !f.IsClassified);
            if (faction is not null && spec.Kind == PublicWantedKind.Fahndung)
            {
                notice.FactionId = faction.Id;
            }

            result.Notices.Add(seeded);
        }
        return result;
    }

    /// <summary>Warning chips: the startup seeder fills an empty table, this adds the ones the demo notices reference.</summary>
    private static async Task<Dictionary<string, Warnhinweis>> EnsureHintsAsync(AppDbContext db, CancellationToken ct)
    {
        var map = (await db.Warnhinweise.ToListAsync(ct))
            .GroupBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        (string Name, string Colour, int Sort)[] wanted =
        [
            ("bewaffnet", "Error", 10),
            ("gewaltbereit", "Error", 20),
            ("flieht mit Fahrzeug", "Warning", 30),
            ("nicht selbst eingreifen", "Info", 40),
        ];
        foreach (var (name, colour, sort) in wanted)
        {
            if (map.ContainsKey(name))
            {
                continue;
            }
            var hint = new Warnhinweis { Name = name, Colour = colour, SortOrder = sort, IsActive = true };
            db.Warnhinweise.Add(hint);
            map[name] = hint;
        }
        return map;
    }

    /// <summary>Bounty shares: one pledged agency share plus, above a threshold, a secured private share with its deposit booking.</summary>
    private async Task<int> SeedBountyAsync(
        AppDbContext db, OeffentlicheFahndung notice, decimal total, DateTime published,
        List<FahndungKopfgeldAnteil> collected, CancellationToken ct)
    {
        var added = 0;
        var agencyShare = total;
        if (total >= 50_000m)
        {
            agencyShare = Math.Round(total * 0.8m, 0);
            var privateShare = total - agencyShare;

            var deposit = new KassenBuchung
            {
                CaseNumber = await caseNumbers.NextAsync(db, KassePrefix, ct),
                Account = KassenKonto.Schwarzgeld,
                Kind = KassenBuchungArt.Einzahlung,
                Amount = privateShare,
                Reason = $"privates Kopfgeld {notice.CaseNumber} · {DemoIdentity.Codename}",
                BookedById = DemoIdentity.AgentId,
                Timestamp = published.AddDays(1),
            };
            db.KassenBuchungen.Add(deposit);
            var privateRow = new FahndungKopfgeldAnteil
            {
                WantedId = notice.Id,
                Origin = BountyOrigin.AgentPrivat,
                Amount = privateShare,
                Account = KassenKonto.Schwarzgeld,
                DonorAgentId = DemoIdentity.AgentId,
                KassenBuchungId = deposit.Id,
                Status = BountyShareStatus.Gesichert,
                Timestamp = published.AddDays(1),
            };
            db.FahndungKopfgeldAnteile.Add(privateRow);
            collected.Add(privateRow);
            added += 2;
        }

        var agencyRow = new FahndungKopfgeldAnteil
        {
            WantedId = notice.Id,
            Origin = BountyOrigin.NooseKasse,
            Amount = agencyShare,
            Account = KassenKonto.Gruengeld,
            DonorAgentId = DemoIdentity.AgentId,
            Status = BountyShareStatus.Zugesagt,
            Timestamp = published,
        };
        db.FahndungKopfgeldAnteile.Add(agencyRow);
        collected.Add(agencyRow);
        return added + 1;
    }

    private sealed record SeededNotice(
        OeffentlicheFahndung Notice, Person Person, decimal Bounty, HazardLevel Hazard, PublicWantedStatus Status)
    {
        /// <summary>The shares created in this run. They are only tracked, so a query would not find them yet.</summary>
        public List<FahndungKopfgeldAnteil> Shares { get; } = [];
    }

    private sealed class PublicSeedResult
    {
        public int Added { get; set; }
        public List<SeededNotice> Notices { get; } = [];
    }

    /// <summary>A row whose CreatedAt the audit interceptor stamps to now; the seeder pushes it back afterwards.</summary>
    private sealed record TimeStamp(string Table, string Id, DateTime CreatedAt);

    private static string Paragraph(string text)
        => "<p>" + System.Net.WebUtility.HtmlEncode(text) + "</p>";

    /// <summary>Pushes CreatedAt back to the demo timeline. Runs after SaveChanges because the audit interceptor stamps every insert with now.</summary>
    private static async Task ApplyTimestampsAsync(AppDbContext db, List<TimeStamp> stamps, CancellationToken ct)
    {
        foreach (var stamp in stamps)
        {
            var id = stamp.Id;
            var at = stamp.CreatedAt;
            switch (stamp.Table)
            {
                case nameof(OeffentlicheFahndung):
                    await db.OeffentlicheFahndungen.IgnoreQueryFilters().Where(x => x.Id == id)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, at), ct);
                    break;
                case nameof(Hinweis):
                    await db.Hinweise.IgnoreQueryFilters().Where(x => x.Id == id)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, at), ct);
                    break;
                case nameof(HinweisNachricht):
                    await db.HinweisNachrichten.IgnoreQueryFilters().Where(x => x.Id == id)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, at), ct);
                    break;
                case nameof(Ticket):
                    await db.Tickets.IgnoreQueryFilters().Where(x => x.Id == id)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, at), ct);
                    break;
                case nameof(TicketNachricht):
                    await db.TicketNachrichten.IgnoreQueryFilters().Where(x => x.Id == id)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, at), ct);
                    break;
                case nameof(FahndungEinspruch):
                    await db.FahndungEinsprueche.IgnoreQueryFilters().Where(x => x.Id == id)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, at), ct);
                    break;
                case nameof(Pressemitteilung):
                    await db.Pressemitteilungen.IgnoreQueryFilters().Where(x => x.Id == id)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, at), ct);
                    break;
                case nameof(OeffentlicheWarnung):
                    await db.OeffentlicheWarnungen.IgnoreQueryFilters().Where(x => x.Id == id)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, at), ct);
                    break;
                case "Feedback":
                    await db.Feedbacks.IgnoreQueryFilters().Where(x => x.Id == id)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, at), ct);
                    break;
            }
        }
    }
}
