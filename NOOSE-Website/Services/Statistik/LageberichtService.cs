using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Statistik;

namespace NOOSE_Website.Services.Statistik;

/// <inheritdoc cref="ILageberichtService" />
public class LageberichtService(
    IDbContextFactory<AppDbContext> dbFactory,
    IStatistikService statistik,
    INotificationService notifications,
    ILogger<LageberichtService> logger) : ILageberichtService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

    public async Task<bool> ErzeugeFaelligenAsync(DateTime jetztUtc, CancellationToken cancellationToken = default)
    {
        // Zuletzt abgeschlossener Monat = Vormonat relativ zu jetzt.
        var vormonat = new DateTime(jetztUtc.Year, jetztUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
        var bericht = await ErzeugeMonatAsync(vormonat.Year, vormonat.Month, ersetzeVorhandene: false,
            ausloeserId: null, cancellationToken);
        return bericht is not null;
    }

    public async Task<Lagebericht?> ErzeugeMonatAsync(int jahr, int monat, bool ersetzeVorhandene, string? ausloeserId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Aktive Berichte des Monats (Soft-Delete-Filter greift). Höchstens einer – defensiv als Liste behandelt.
        var vorhandene = await db.Lageberichte
            .Where(l => l.Jahr == jahr && l.Monat == monat)
            .ToListAsync(cancellationToken);
        if (vorhandene.Count > 0)
        {
            if (!ersetzeVorhandene)
            {
                return null;
            }
            // Ersetzen: alte aktive Berichte des Monats in den Papierkorb verschieben.
            foreach (var alt in vorhandene)
            {
                alt.IstGeloescht = true;
                alt.GeloeschtAm = DateTime.UtcNow;
                alt.GeloeschtVonId = ausloeserId;
            }
        }

        // Vollständige Lage als Schnappschuss (istFuehrung: true → inkl. VS-Aggregate; Bericht ist Führung
        // vorbehalten). meId: null genügt – die Führung sieht ohnehin alle Taskforces.
        var report = await statistik.GetReportAsync(istFuehrung: true, meId: null, cancellationToken: cancellationToken);
        var titel = $"Lagebericht {new DateTime(jahr, monat, 1).ToString("MMMM yyyy", DeDe)}";

        var bericht = new Lagebericht
        {
            Jahr = jahr,
            Monat = monat,
            Titel = titel,
            SnapshotJson = JsonSerializer.Serialize(report, JsonOptions),
            ErstelltVonId = ausloeserId,
        };
        db.Lageberichte.Add(bericht);
        await db.SaveChangesAsync(cancellationToken);

        await BenachrichtigeFuehrungAsync(db, bericht, titel, ausloeserId, cancellationToken);
        return bericht;
    }

    public async Task<List<LageberichtKopf>> GetArchivAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var rows = await db.Lageberichte
            .OrderByDescending(l => l.Jahr).ThenByDescending(l => l.Monat).ThenByDescending(l => l.ErstelltAm)
            .Select(l => new { l.Id, l.Jahr, l.Monat, l.Titel, l.ErstelltAm, l.ErstelltVonId })
            .ToListAsync(cancellationToken);

        // Ersteller-Codenamen (öffentlich, nie Klarname) in einem Rutsch auflösen.
        var erstellerIds = rows.Where(r => !string.IsNullOrEmpty(r.ErstelltVonId))
            .Select(r => r.ErstelltVonId!).Distinct().ToList();
        var namen = erstellerIds.Count == 0
            ? new Dictionary<string, string>()
            : await db.Users.Where(u => erstellerIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Codename }).ToDictionaryAsync(u => u.Id, u => u.Codename, cancellationToken);

        return rows.Select(r => new LageberichtKopf(r.Id, r.Jahr, r.Monat, r.Titel, r.ErstelltAm,
            string.IsNullOrEmpty(r.ErstelltVonId) ? null : namen.GetValueOrDefault(r.ErstelltVonId))).ToList();
    }

    public async Task<LageberichtAnzeige?> GetAnzeigeAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var bericht = await db.Lageberichte.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (bericht is null)
        {
            return null;
        }

        StatistikReport? report;
        try
        {
            report = JsonSerializer.Deserialize<StatistikReport>(bericht.SnapshotJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Lagebericht {Id} hat einen unlesbaren Snapshot.", id);
            return null;
        }
        if (report is null)
        {
            return null;
        }

        string? erzeugtVon = null;
        if (!string.IsNullOrEmpty(bericht.ErstelltVonId))
        {
            erzeugtVon = await db.Users.Where(u => u.Id == bericht.ErstelltVonId)
                .Select(u => u.Codename).FirstOrDefaultAsync(cancellationToken);
        }

        return new LageberichtAnzeige(bericht.Id, bericht.Titel, bericht.ErstelltAm, erzeugtVon, report);
    }

    public async Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var bericht = await db.Lageberichte.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (bericht is null)
        {
            return;
        }
        bericht.IstGeloescht = true;
        bericht.GeloeschtAm = DateTime.UtcNow;
        bericht.GeloeschtVonId = handelnder.GetAgentId();
        await db.SaveChangesAsync(cancellationToken);
    }

    // Glocken-Benachrichtigung an die (rang-basierte) Führung – best-effort, nach dem Commit. Rein rollen-
    // basierte Admins (ohne Führungs-Dienstgrad) sind bewusst nicht enthalten; ein fehlgeschlagener Push
    // darf die Erzeugung nie scheitern lassen.
    private async Task BenachrichtigeFuehrungAsync(AppDbContext db, Lagebericht bericht, string titel,
        string? ausloeserId, CancellationToken cancellationToken)
    {
        try
        {
            var fuehrungIds = await db.Users
                .Where(u => u.Status == AgentStatus.Aktiv && u.Dienstgrad != null
                    && u.Dienstgrad >= Dienstgrad.SupervisorySpecialAgent)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            await notifications.BenachrichtigeVieleAsync(fuehrungIds, NotificationTyp.Lagebericht,
                $"Neuer {titel}", $"/lageberichte/{bericht.Id}", ausloeserId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Benachrichtigung über den neuen Lagebericht {Id} fehlgeschlagen.", bericht.Id);
        }
    }
}
