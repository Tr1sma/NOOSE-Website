using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Operationen;
using NOOSE_Website.Data.Entities.Parteien;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Vorgaenge;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Dashboard;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IDashboardService" />
public class DashboardService(IDbContextFactory<AppDbContext> dbFactory, IAntragService antragService) : IDashboardService
{
    public async Task<DashboardKennzahlen> GetKennzahlenAsync(bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Der globale Soft-Delete-Filter blendet Papierkorb-Akten automatisch aus. Die VS-Bedingung
        // spiegelt die jeweilige Listenansicht, damit die Kachel exakt deren Trefferzahl zeigt.
        var personen = await db.Personen.CountAsync(p => istFuehrung || !p.IstVerschlusssache, cancellationToken);
        var fraktionen = await db.Fraktionen.CountAsync(f => istFuehrung || !f.IstVerschlusssache, cancellationToken);
        var gruppen = await db.Personengruppen.CountAsync(g => istFuehrung || !g.IstVerschlusssache, cancellationToken);
        var parteien = await db.Parteien.CountAsync(p => istFuehrung || !p.IstVerschlusssache, cancellationToken);
        var operationen = await db.Operationen.CountAsync(o => istFuehrung || !o.IstVerschlusssache, cancellationToken);

        // Offene Vorgänge = noch nicht abgeschlossene/archivierte Fälle (Offen/In Bearbeitung/Ruht), VS-gefiltert.
        var offeneVorgaenge = await db.Vorgaenge.CountAsync(v => (istFuehrung || !v.IstVerschlusssache)
            && v.Status != VorgangStatus.Abgeschlossen && v.Status != VorgangStatus.Archiviert, cancellationToken);

        // Offene Anträge = Hochstufungs-Anträge + ausstehende Registrierungen + offene Namensänderungen
        // + beantragte Taskforces + Beförderungsanträge (alle im Freigabe-Posteingang). Die Hochstufungs-
        // Anträge laufen über den VS-gefilterten Dienst (wie NavMenu-Badge + Posteingang), beantragte
        // Verschlusssache-Taskforces zählen nur für die Führung.
        var offeneAntraege = await antragService.GetOffeneAnzahlAsync(istFuehrung, cancellationToken)
            + await db.Users.CountAsync(a => a.Status == AgentStatus.Ausstehend, cancellationToken)
            + await db.Users.CountAsync(a => a.NamensaenderungBeantragtAm != null, cancellationToken)
            + await db.Taskforces.CountAsync(t => t.Status == TaskforceStatus.Beantragt && (istFuehrung || !t.IstVerschlusssache), cancellationToken)
            + await db.AgentBefoerderungsantraege.CountAsync(a => a.Status == BefoerderungStatus.Beantragt, cancellationToken);

        // Anzahl klassifizierter Akten ist selbst eine Verschlusssache → nur für die Führung.
        var verschlusssachen = 0;
        if (istFuehrung)
        {
            verschlusssachen =
                  await db.Personen.CountAsync(p => p.IstVerschlusssache, cancellationToken)
                + await db.Fraktionen.CountAsync(f => f.IstVerschlusssache, cancellationToken)
                + await db.Personengruppen.CountAsync(g => g.IstVerschlusssache, cancellationToken)
                + await db.Parteien.CountAsync(p => p.IstVerschlusssache, cancellationToken)
                + await db.Operationen.CountAsync(o => o.IstVerschlusssache, cancellationToken)
                + await db.Taskforces.CountAsync(t => t.IstVerschlusssache, cancellationToken)
                + await db.Vorgaenge.CountAsync(v => v.IstVerschlusssache, cancellationToken);
        }

        // Die Org-Kachel bündelt Fraktionen, Personengruppen und Parteien; Operationen sind eine eigene Kachel.
        return new DashboardKennzahlen(personen, fraktionen + gruppen + parteien, operationen, offeneVorgaenge, offeneAntraege, verschlusssachen);
    }

    public async Task<DashboardVerteilungen> GetVerteilungenAsync(bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Alle vier Verteilungen sind – wie die Kennzahl-Kacheln – VS-gefiltert: Nicht-Führung zählt nur
        // nicht-klassifizierte Akten, damit aus den Diagrammen kein Verschlusssachen-Bestand ablesbar ist.

        // 1) Fälle (Vorgänge) nach Einstufung. Alle Enum-Werte werden gefüllt (fehlende = 0 → stabile Legende).
        var einstufungZaehlung = (await db.Vorgaenge
                .Where(v => istFuehrung || !v.IstVerschlusssache)
                .GroupBy(v => v.Einstufung)
                .Select(g => new { Wert = g.Key, Anzahl = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.Wert, x => x.Anzahl);
        var faelleNachEinstufung = EinstufungAnzeige.Alle
            .Select(e => new VerteilungSegment(EinstufungAnzeige.Name(e), einstufungZaehlung.GetValueOrDefault(e)))
            .ToList();

        // 2) Maßnahme-Ausgänge der Personen-Doks. VS-Filter über die Eltern-Person (Referenz-Navigation →
        //    INNER JOIN, dessen Soft-Delete-Filter zugleich Doks gelöschter Personen ausblendet).
        var ausgangZaehlung = (await db.PersonDoks
                .Where(d => istFuehrung || !d.Person!.IstVerschlusssache)
                .GroupBy(d => d.Ausgang)
                .Select(g => new { Wert = g.Key, Anzahl = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.Wert, x => x.Anzahl);
        var massnahmeAusgaenge = MassnahmeAusgangAnzeige.Alle
            .Select(a => new VerteilungSegment(MassnahmeAusgangAnzeige.Name(a), ausgangZaehlung.GetValueOrDefault(a)))
            .ToList();

        // 3) Fraktionen nach Gefährdung – on-read aus dem (Phase-8-)Bedrohungs-Score abgeleitet. Da der Score
        //    aktuell für alle Fraktionen null ist, landen vorerst alle in „Keine". Bucketing in-memory (kleine
        //    Menge, vermeidet eine CASE-Übersetzung).
        var scores = await db.Fraktionen
            .Where(f => istFuehrung || !f.IstVerschlusssache)
            .Select(f => f.BedrohungsScore)
            .ToListAsync(cancellationToken);
        var gefaehrdungZaehlung = scores
            .GroupBy(GefaehrdungsStufeLogic.Aus)
            .ToDictionary(g => g.Key, g => g.Count());
        var fraktionenNachGefaehrdung = GefaehrdungsStufeLogic.Alle
            .Select(s => new VerteilungSegment(GefaehrdungsStufeLogic.Name(s), gefaehrdungZaehlung.GetValueOrDefault(s)))
            .ToList();

        // 4) Offene Anträge nach Art – exakt dieselben fünf Teilzähler wie die KPI-Kachel „Offene Anträge"
        //    (GetKennzahlenAsync), nur einzeln ausgewiesen; die Summe entspricht damit der Kachel.
        var offeneAntraegeNachArt = new List<VerteilungSegment>
        {
            new("Hochstufung", await antragService.GetOffeneAnzahlAsync(istFuehrung, cancellationToken)),
            new("Registrierung", await db.Users.CountAsync(a => a.Status == AgentStatus.Ausstehend, cancellationToken)),
            new("Namensänderung", await db.Users.CountAsync(a => a.NamensaenderungBeantragtAm != null, cancellationToken)),
            new("Taskforce", await db.Taskforces.CountAsync(t => t.Status == TaskforceStatus.Beantragt && (istFuehrung || !t.IstVerschlusssache), cancellationToken)),
            new("Beförderung", await db.AgentBefoerderungsantraege.CountAsync(a => a.Status == BefoerderungStatus.Beantragt, cancellationToken)),
        };

        return new DashboardVerteilungen(faelleNachEinstufung, massnahmeAusgaenge, fraktionenNachGefaehrdung, offeneAntraegeNachArt);
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

        // IgnoreQueryFilters: ein Austritt ist ein Soft-Delete der Mitgliedschaft – ohne dies fiele die Zeile
        // aus dem Lookup und das „Mitglied entfernt"-Ereignis ließe sich nie auf eine Akte abbilden.
        var fmIds = Ids(roh, nameof(FraktionMitglied));
        var mitgliedZuFraktion = fmIds.Count == 0 ? new Dictionary<string, string>()
            : await db.FraktionMitglieder.IgnoreQueryFilters().Where(m => fmIds.Contains(m.Id))
                .Select(m => new { m.Id, m.FraktionId }).ToDictionaryAsync(x => x.Id, x => x.FraktionId, cancellationToken);

        var pmIds = Ids(roh, nameof(PersonengruppeMitglied));
        var mitgliedZuGruppe = pmIds.Count == 0 ? new Dictionary<string, string>()
            : await db.PersonengruppeMitglieder.IgnoreQueryFilters().Where(m => pmIds.Contains(m.Id))
                .Select(m => new { m.Id, m.PersonengruppeId }).ToDictionaryAsync(x => x.Id, x => x.PersonengruppeId, cancellationToken);

        var paIds = Ids(roh, nameof(PersonengruppeAgent));
        var agentZuGruppe = paIds.Count == 0 ? new Dictionary<string, string>()
            : await db.PersonengruppeAgenten.Where(a => paIds.Contains(a.Id))
                .Select(a => new { a.Id, a.PersonengruppeId }).ToDictionaryAsync(x => x.Id, x => x.PersonengruppeId, cancellationToken);

        var pmParteiIds = Ids(roh, nameof(ParteiMitglied));
        var mitgliedZuPartei = pmParteiIds.Count == 0 ? new Dictionary<string, string>()
            : await db.ParteiMitglieder.IgnoreQueryFilters().Where(m => pmParteiIds.Contains(m.Id))
                .Select(m => new { m.Id, m.ParteiId }).ToDictionaryAsync(x => x.Id, x => x.ParteiId, cancellationToken);

        var paParteiIds = Ids(roh, nameof(ParteiAgent));
        var agentZuPartei = paParteiIds.Count == 0 ? new Dictionary<string, string>()
            : await db.ParteiAgenten.Where(a => paParteiIds.Contains(a.Id))
                .Select(a => new { a.Id, a.ParteiId }).ToDictionaryAsync(x => x.Id, x => x.ParteiId, cancellationToken);

        var oaIds = Ids(roh, nameof(OperationAgent));
        var agentZuOperation = oaIds.Count == 0 ? new Dictionary<string, string>()
            : await db.OperationAgenten.Where(a => oaIds.Contains(a.Id))
                .Select(a => new { a.Id, a.OperationId }).ToDictionaryAsync(x => x.Id, x => x.OperationId, cancellationToken);

        var taIds = Ids(roh, nameof(TaskforceAgent));
        var agentZuTaskforce = taIds.Count == 0 ? new Dictionary<string, string>()
            : await db.TaskforceAgenten.Where(a => taIds.Contains(a.Id))
                .Select(a => new { a.Id, a.TaskforceId }).ToDictionaryAsync(x => x.Id, x => x.TaskforceId, cancellationToken);

        var vaIds = Ids(roh, nameof(VorgangAgent));
        var agentZuVorgang = vaIds.Count == 0 ? new Dictionary<string, string>()
            : await db.VorgangAgenten.Where(a => vaIds.Contains(a.Id))
                .Select(a => new { a.Id, a.VorgangId }).ToDictionaryAsync(x => x.Id, x => x.VorgangId, cancellationToken);

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
                nameof(Partei) => (DashboardAkteTyp.Partei, log.EntitaetId, null),
                nameof(ParteiMitglied) when mitgliedZuPartei.TryGetValue(log.EntitaetId, out var prid)
                    => (DashboardAkteTyp.Partei, prid, "Mitglied"),
                nameof(ParteiAgent) when agentZuPartei.TryGetValue(log.EntitaetId, out var prid2)
                    => (DashboardAkteTyp.Partei, prid2, "Agent-Zuteilung"),
                nameof(Operation) => (DashboardAkteTyp.Operation, log.EntitaetId, null),
                nameof(OperationAgent) when agentZuOperation.TryGetValue(log.EntitaetId, out var oid)
                    => (DashboardAkteTyp.Operation, oid, "Agent-Zuteilung"),
                nameof(Taskforce) => (DashboardAkteTyp.Taskforce, log.EntitaetId, null),
                nameof(TaskforceAgent) when agentZuTaskforce.TryGetValue(log.EntitaetId, out var tid)
                    => (DashboardAkteTyp.Taskforce, tid, "Agent-Zuteilung"),
                nameof(Vorgang) => (DashboardAkteTyp.Vorgang, log.EntitaetId, null),
                nameof(VorgangAgent) when agentZuVorgang.TryGetValue(log.EntitaetId, out var vid)
                    => (DashboardAkteTyp.Vorgang, vid, "Agent-Zuteilung"),
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
        var parteiMap = await ParteiInfos(db, ZielIds(ziele, DashboardAkteTyp.Partei), cancellationToken);
        var operationMap = await OperationInfos(db, ZielIds(ziele, DashboardAkteTyp.Operation), cancellationToken);
        var taskforceMap = await TaskforceInfos(db, ZielIds(ziele, DashboardAkteTyp.Taskforce), cancellationToken);
        var vorgangMap = await VorgangInfos(db, ZielIds(ziele, DashboardAkteTyp.Vorgang), cancellationToken);

        var ergebnis = new List<DashboardAenderung>();
        foreach (var (log, typ, akteId, detail) in ziele)
        {
            var info = typ switch
            {
                DashboardAkteTyp.Person => personMap.GetValueOrDefault(akteId),
                DashboardAkteTyp.Fraktion => fraktionMap.GetValueOrDefault(akteId),
                DashboardAkteTyp.Partei => parteiMap.GetValueOrDefault(akteId),
                DashboardAkteTyp.Operation => operationMap.GetValueOrDefault(akteId),
                DashboardAkteTyp.Taskforce => taskforceMap.GetValueOrDefault(akteId),
                DashboardAkteTyp.Vorgang => vorgangMap.GetValueOrDefault(akteId),
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

    private static async Task<Dictionary<string, AkteInfo>> ParteiInfos(AppDbContext db, List<string> ids, CancellationToken ct)
        => ids.Count == 0 ? new()
            : await db.Parteien.IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => new AkteInfo(p.Name, p.Aktenzeichen, p.IstVerschlusssache, p.IstGeloescht), ct);

    private static async Task<Dictionary<string, AkteInfo>> OperationInfos(AppDbContext db, List<string> ids, CancellationToken ct)
        => ids.Count == 0 ? new()
            : await db.Operationen.IgnoreQueryFilters().Where(o => ids.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, o => new AkteInfo(o.Titel, o.Aktenzeichen, o.IstVerschlusssache, o.IstGeloescht), ct);

    private static async Task<Dictionary<string, AkteInfo>> TaskforceInfos(AppDbContext db, List<string> ids, CancellationToken ct)
        => ids.Count == 0 ? new()
            : await db.Taskforces.IgnoreQueryFilters().Where(t => ids.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => new AkteInfo(t.Name, t.Aktenzeichen, t.IstVerschlusssache, t.IstGeloescht), ct);

    private static async Task<Dictionary<string, AkteInfo>> VorgangInfos(AppDbContext db, List<string> ids, CancellationToken ct)
        => ids.Count == 0 ? new()
            : await db.Vorgaenge.IgnoreQueryFilters().Where(v => ids.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, v => new AkteInfo(v.Titel, v.Aktenzeichen, v.IstVerschlusssache, v.IstGeloescht), ct);
}
