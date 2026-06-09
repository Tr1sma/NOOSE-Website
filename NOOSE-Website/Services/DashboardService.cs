using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Dashboard;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IDashboardService" />
public class DashboardService(IDbContextFactory<AppDbContext> dbFactory) : IDashboardService
{
    public async Task<DashboardKennzahlen> GetKennzahlenAsync(bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Der globale Soft-Delete-Filter blendet Papierkorb-Akten automatisch aus. Die VS-Bedingung
        // spiegelt die jeweilige Listenansicht, damit die Kachel exakt deren Trefferzahl zeigt.
        var personen = await db.Personen.CountAsync(p => istFuehrung || !p.IstVerschlusssache, cancellationToken);
        var fraktionen = await db.Fraktionen.CountAsync(f => istFuehrung || !f.IstVerschlusssache, cancellationToken);
        var gruppen = await db.Personengruppen.CountAsync(g => istFuehrung || !g.IstVerschlusssache, cancellationToken);

        var offeneAntraege = await db.Users.CountAsync(a => a.Status == AgentStatus.Ausstehend, cancellationToken);

        // Anzahl klassifizierter Akten ist selbst eine Verschlusssache → nur für die Führung.
        var verschlusssachen = 0;
        if (istFuehrung)
        {
            verschlusssachen =
                  await db.Personen.CountAsync(p => p.IstVerschlusssache, cancellationToken)
                + await db.Fraktionen.CountAsync(f => f.IstVerschlusssache, cancellationToken)
                + await db.Personengruppen.CountAsync(g => g.IstVerschlusssache, cancellationToken);
        }

        return new DashboardKennzahlen(personen, fraktionen + gruppen, offeneAntraege, verschlusssachen);
    }

    public async Task<List<DashboardAenderung>> GetLetzteAenderungenAsync(bool istFuehrung, int max = 8, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Großzügig laden: VS-Filter und nicht auflösbare Einträge (z. B. hart entfernte Mitglieder)
        // dünnen die Liste noch aus, bevor wir auf `max` kürzen.
        var roh = await db.AuditLogs
            .OrderByDescending(a => a.Zeitpunkt)
            .ThenByDescending(a => a.Id)
            .Take(Math.Max(max, 1) * 8)
            .ToListAsync(cancellationToken);

        if (roh.Count == 0)
        {
            return new List<DashboardAenderung>();
        }

        // Kind-Entitäten auf ihre Eltern-Akte hochrollen (je ein server-seitig gefilterter Batch-Lookup).
        var dokIds = Ids(roh, nameof(PersonDok));
        var dokZuPerson = dokIds.Count == 0 ? new Dictionary<string, string>()
            : await db.PersonDoks.IgnoreQueryFilters().Where(d => dokIds.Contains(d.Id))
                .Select(d => new { d.Id, d.PersonId }).ToDictionaryAsync(x => x.Id, x => x.PersonId, cancellationToken);

        var fmIds = Ids(roh, nameof(FraktionMitglied));
        var mitgliedZuFraktion = fmIds.Count == 0 ? new Dictionary<string, string>()
            : await db.FraktionMitglieder.Where(m => fmIds.Contains(m.Id))
                .Select(m => new { m.Id, m.FraktionId }).ToDictionaryAsync(x => x.Id, x => x.FraktionId, cancellationToken);

        var pmIds = Ids(roh, nameof(PersonengruppeMitglied));
        var mitgliedZuGruppe = pmIds.Count == 0 ? new Dictionary<string, string>()
            : await db.PersonengruppeMitglieder.Where(m => pmIds.Contains(m.Id))
                .Select(m => new { m.Id, m.PersonengruppeId }).ToDictionaryAsync(x => x.Id, x => x.PersonengruppeId, cancellationToken);

        var paIds = Ids(roh, nameof(PersonengruppeAgent));
        var agentZuGruppe = paIds.Count == 0 ? new Dictionary<string, string>()
            : await db.PersonengruppeAgenten.Where(a => paIds.Contains(a.Id))
                .Select(a => new { a.Id, a.PersonengruppeId }).ToDictionaryAsync(x => x.Id, x => x.PersonengruppeId, cancellationToken);

        // Jeden Audit-Eintrag (in Reihenfolge) auf eine Ziel-Akte abbilden – oder verwerfen.
        var ziele = new List<(AuditLog Log, DashboardAkteTyp Typ, string AkteId, string? Detail)>();
        foreach (var log in roh)
        {
            (DashboardAkteTyp Typ, string AkteId, string? Detail)? ziel = log.EntitaetTyp switch
            {
                nameof(Person) => (DashboardAkteTyp.Person, log.EntitaetId, (string?)null),
                nameof(Fraktion) => (DashboardAkteTyp.Fraktion, log.EntitaetId, null),
                nameof(Personengruppe) => (DashboardAkteTyp.Personengruppe, log.EntitaetId, null),
                nameof(PersonDok) when dokZuPerson.TryGetValue(log.EntitaetId, out var pid)
                    => (DashboardAkteTyp.Person, pid, "Dok"),
                nameof(FraktionMitglied) when mitgliedZuFraktion.TryGetValue(log.EntitaetId, out var fid)
                    => (DashboardAkteTyp.Fraktion, fid, "Mitglied"),
                nameof(PersonengruppeMitglied) when mitgliedZuGruppe.TryGetValue(log.EntitaetId, out var gid)
                    => (DashboardAkteTyp.Personengruppe, gid, "Mitglied"),
                nameof(PersonengruppeAgent) when agentZuGruppe.TryGetValue(log.EntitaetId, out var gid2)
                    => (DashboardAkteTyp.Personengruppe, gid2, "Agent-Zuteilung"),
                _ => null,
            };

            if (ziel is { } z)
            {
                ziele.Add((log, z.Typ, z.AkteId, z.Detail));
            }
        }

        // Anzeigedaten der Akten in einem Rutsch laden (inkl. Papierkorb → „gelöscht" bleibt benennbar).
        var personMap = await PersonInfos(db, ZielIds(ziele, DashboardAkteTyp.Person), cancellationToken);
        var fraktionMap = await FraktionInfos(db, ZielIds(ziele, DashboardAkteTyp.Fraktion), cancellationToken);
        var gruppeMap = await GruppeInfos(db, ZielIds(ziele, DashboardAkteTyp.Personengruppe), cancellationToken);

        var ergebnis = new List<DashboardAenderung>();
        foreach (var (log, typ, akteId, detail) in ziele)
        {
            var info = typ switch
            {
                DashboardAkteTyp.Person => personMap.GetValueOrDefault(akteId),
                DashboardAkteTyp.Fraktion => fraktionMap.GetValueOrDefault(akteId),
                _ => gruppeMap.GetValueOrDefault(akteId),
            };

            // Akte nicht mehr auffindbar (z. B. hart entfernt) oder Verschlusssache ohne Berechtigung.
            if (info is null || (info.IstVerschlusssache && !istFuehrung))
            {
                continue;
            }

            ergebnis.Add(new DashboardAenderung(
                log.Zeitpunkt, log.AgentName, log.Aktion, typ,
                akteId, info.Name, info.Aktenzeichen, detail, info.IstGeloescht));

            if (ergebnis.Count >= max)
            {
                break;
            }
        }

        return ergebnis;
    }

    // ---- Helfer ----

    private sealed record AkteInfo(string Name, string Aktenzeichen, bool IstVerschlusssache, bool IstGeloescht);

    private static List<string> Ids(List<AuditLog> logs, string typ)
        => logs.Where(a => a.EntitaetTyp == typ).Select(a => a.EntitaetId).Distinct().ToList();

    private static List<string> ZielIds(
        IEnumerable<(AuditLog Log, DashboardAkteTyp Typ, string AkteId, string? Detail)> ziele, DashboardAkteTyp typ)
        => ziele.Where(z => z.Typ == typ).Select(z => z.AkteId).Distinct().ToList();

    private static async Task<Dictionary<string, AkteInfo>> PersonInfos(AppDbContext db, List<string> ids, CancellationToken ct)
        => ids.Count == 0 ? new()
            : await db.Personen.IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => new AkteInfo(p.Name, p.Aktenzeichen, p.IstVerschlusssache, p.IstGeloescht), ct);

    private static async Task<Dictionary<string, AkteInfo>> FraktionInfos(AppDbContext db, List<string> ids, CancellationToken ct)
        => ids.Count == 0 ? new()
            : await db.Fraktionen.IgnoreQueryFilters().Where(f => ids.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => new AkteInfo(f.Name, f.Aktenzeichen, f.IstVerschlusssache, f.IstGeloescht), ct);

    private static async Task<Dictionary<string, AkteInfo>> GruppeInfos(AppDbContext db, List<string> ids, CancellationToken ct)
        => ids.Count == 0 ? new()
            : await db.Personengruppen.IgnoreQueryFilters().Where(g => ids.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => new AkteInfo(g.Name, g.Aktenzeichen, g.IstVerschlusssache, g.IstGeloescht), ct);
}
