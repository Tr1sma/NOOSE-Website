using System.Reflection;
using System.Runtime.CompilerServices;
using NOOSE_Website.Authorization;
using Microsoft.Extensions.Logging;
using NOOSE_Website.Data;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Infrastructure.Notifications;
using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Navigation;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>The registries nothing used to enforce.</summary>
/// <remarks>
/// Each of these has burnt someone once. A new audited entity showed its raw CLR name in the German UI of /nachweis
/// (BuergerProfil, until phase 2). A public route that was not a child of /gesucht would have been missed in the demo
/// middleware. And a partner saw "not released for partner agencies" on a page he can open logged out.
/// </remarks>
public class PublicSurfaceGuardTests
{
    private static string DisplayFile([CallerFilePath] string here = "")
    {
        var file = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..",
            "NOOSE-Website", "Services", "AuditEntityDisplay.cs"));
        Assert.True(File.Exists(file), $"AuditEntityDisplay nicht gefunden: {file}");
        return file;
    }

    private static IEnumerable<Type> EntityTypes()
        => typeof(AppDbContext).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsGenericType
                && p.PropertyType.GetGenericTypeDefinition() == typeof(Microsoft.EntityFrameworkCore.DbSet<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .Distinct();

    [Fact]
    public void EveryAuditablePublicAreaEntity_HasALabelAndARoute()
    {
        // The interceptor stamps the CLR name; without an arm here it is what /nachweis shows a German-speaking user
        // — BuergerProfil went out that way until phase 2. Scoped to the public area rather than the whole context on
        // purpose: about seventy internal child tables have run without a label since long before this, and fixing
        // that is its own piece of work, not a side effect of a public-area phase.
        // the label is checked against the source rather than against Label(name) != name: "Warnhinweis" happens to
        // read the same in both languages, so a value comparison would call the correct arm a miss
        var source = File.ReadAllText(DisplayFile());
        var offenders = EntityTypes()
            .Where(t => t.Namespace == "NOOSE_Website.Data.Entities.Public")
            .Where(t => typeof(IAuditable).IsAssignableFrom(t))
            .Where(t => !source.Contains($"\"{t.Name}\" =>", StringComparison.Ordinal)
                || AuditEntityDisplay.Route(t.Name, "id") is null)
            .Select(t => t.Name)
            .Order()
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Jede auditierte Entität des öffentlichen Bereichs braucht Label und Route in AuditEntityDisplay: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void EverySettingsRouteOfTheAuditDisplay_NamesAnExistingSection()
    {
        // a route into /einstellungen is only useful if the rail actually has that section
        var offenders = EntityTypes()
            .Select(t => AuditEntityDisplay.Route(t.Name, "id"))
            .Where(r => r is not null && r.StartsWith("/einstellungen?tab=", StringComparison.Ordinal))
            .Select(r => r!["/einstellungen?tab=".Length..])
            .Distinct(StringComparer.Ordinal)
            .Where(slug => !MergedPageSections.Settings.Contains(slug, StringComparer.Ordinal))
            .Order()
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Jede ?tab=-Route zeigt auf einen vorhandenen Abschnitt: " + string.Join(", ", offenders));
    }

    [Fact]
    public void EveryAuditablePublicAreaEntity_IsDecidedInTheWatchlistRollup()
    {
        // the rollup's default arm only warns, so an unlisted type is a log line per write and nothing else — every
        // public-area table had been running that way since phase 1, found by reading the server log
        var logger = new CountingLogger();
        var offenders = EntityTypes()
            .Where(t => t.Namespace == "NOOSE_Website.Data.Entities.Public")
            .Where(t => typeof(IAuditable).IsAssignableFrom(t))
            .Where(t =>
            {
                logger.Warnings = 0;
                WatchlistRecordRollup.Map(Activator.CreateInstance(t)!, logger);
                return logger.Warnings > 0;
            })
            .Select(t => t.Name)
            .Order()
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Jede auditierte Entität des öffentlichen Bereichs ist im Watchlist-Rollup entschieden: "
            + string.Join(", ", offenders));
    }

    private sealed class CountingLogger : ILogger
    {
        public int Warnings { get; set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
            {
                Warnings++;
            }
        }
    }

    [Fact]
    public void EveryPublicRoutePrefix_IsExcludedFromDemoMode()
    {
        // /gesucht used to be listed by hand; /gefasst is a sibling route, not a child of it, and would have been
        // missed the same way — an anonymous archive visitor would then carry the demo agent
        var offenders = PublicRoutes.Prefixes
            .Where(p => !DemoModeMiddleware.ExcludedPrefixes.Contains(p, StringComparer.OrdinalIgnoreCase))
            .Order()
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Jede öffentliche Route bleibt im Demo-Modus anonym: " + string.Join(", ", offenders));
    }

    [Fact]
    public void PartnerRoutes_NeverBlockAPublicRoute()
    {
        // a partner can open the same page logged out, so the refusal would claim a restriction that does not exist
        var offenders = PublicRoutes.Prefixes
            .Where(p => !PartnerRoutes.IsAllowed(p))
            .Order()
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Eine öffentliche Route ist für einen Partner nie gesperrt: " + string.Join(", ", offenders));
    }

    private static string ProjectRoot([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", "NOOSE-Website"));

    /// <summary>Names that would turn the advertised sum into a breakdown of who paid what.</summary>
    private static readonly string[] BountyInternals =
    [
        "IBountyService", "BountyShareRow", "BountySummary", "BountyCoverage",
        "BountyOrigin", "BountyShareStatus", "KassenKonto", "DonorAgentId", "KassenBuchungId",
        // a payout breakdown is the same class of leak: who was paid how much out of whose money
        "IRewardService", "RewardRow", "RewardDraft",
    ];

    [Fact]
    public void NoAnonymousPage_NamesTheBountyBreakdown()
    {
        // The outside gets one number. Origin, donor, account and share count are the part that would say which agent
        // staked his own money on whom — a structural rule (PublicBounty cannot carry them) plus this scan, because
        // a page could still reach past the record and query the service itself.
        var pages = Path.Combine(ProjectRoot(), "Components", "Pages", "Public");
        Assert.True(Directory.Exists(pages), $"Öffentliche Seiten nicht gefunden: {pages}");

        var offenders = Directory.EnumerateFiles(pages, "*.razor", SearchOption.AllDirectories)
            .Select(f => (File: Path.GetFileName(f), Text: File.ReadAllText(f)))
            .SelectMany(f => BountyInternals
                .Where(name => f.Text.Contains(name, StringComparison.Ordinal))
                .Select(name => $"{f.File}: {name}"))
            .Order()
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Nach außen geht nur die Summe: " + string.Join(", ", offenders));
    }

    [Fact]
    public void EveryWriterOfTheBountyTable_DropsThePublicSnapshot()
    {
        // The share table feeds the cached snapshot but is owned by another service, so the one thing that must not be
        // forgettable is the invalidation. PublicWantedService satisfies this by declaring the method itself.
        var offenders = Directory
            .EnumerateFiles(ProjectRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.Combine("Data", "Migrations"), StringComparison.Ordinal))
            .Select(f => (File: Path.GetFileName(f), Text: File.ReadAllText(f)))
            .Where(f => f.Text.Contains("FahndungKopfgeldAnteile", StringComparison.Ordinal)
                && f.Text.Contains("SaveChangesAsync", StringComparison.Ordinal)
                && !f.Text.Contains("InvalidatePublicViewAsync", StringComparison.Ordinal))
            .Select(f => f.File)
            .Order()
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Wer Kopfgeld-Anteile schreibt, verwirft den öffentlichen Snapshot: " + string.Join(", ", offenders));
    }

    /// <summary>Names that would put an agent behind a citizen-facing message.</summary>
    private static readonly string[] DeskInternals =
    [
        "AuthorAgentId", "AuthorCodename", "TicketMessageRow", "TicketRow", "TicketDetail",
        "TipMessageRow", "TipRow", "TipDetail",
    ];

    [Fact]
    public void NoCitizenPage_NamesTheHandlerSideOfAConversation()
    {
        // The agency answers under one constant name. The outward records structurally carry no author, but a
        // portal page could still reach past them and ask the service for the handler projection.
        var pages = Path.Combine(ProjectRoot(), "Components", "Pages", "Portal");
        Assert.True(Directory.Exists(pages), $"Bürgerseiten nicht gefunden: {pages}");

        // whole identifiers only: the outward records are named CitizenTicketDetail and CitizenTipDetail, and a
        // substring match would report the very types that keep the promise
        var offenders = Directory.EnumerateFiles(pages, "*.razor", SearchOption.AllDirectories)
            .Select(f => (File: Path.GetFileName(f), Text: File.ReadAllText(f)))
            .SelectMany(f => DeskInternals
                .Where(name => System.Text.RegularExpressions.Regex.IsMatch(f.Text, $@"\b{name}\b"))
                .Select(name => $"{f.File}: {name}"))
            .Order()
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Der Absender nach außen ist eine Konstante, kein Agent: " + string.Join(", ", offenders));
    }
}
