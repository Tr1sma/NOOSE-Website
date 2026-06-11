using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Ankuendigungen;
using NOOSE_Website.Models.Ankuendigungen;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IAnkuendigungService" />
public class AnkuendigungService(
    IDbContextFactory<AppDbContext> dbFactory,
    IAktenzeichenService aktenzeichen,
    INotificationService notifications) : IAnkuendigungService
{
    public async Task<List<AnkuendigungZeile>> GetBrettAsync(ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var meId = handelnder.GetAgentId();
        var istFuehrung = handelnder.IstFuehrung();
        var istTRU = handelnder.IstTRU();
        var meinDienstgrad = handelnder.GetDienstgrad();

        // Taskforces des Betrachters – für die Sichtbarkeit der Taskforce-Zielgruppe (flaches WHERE FK IN).
        var meineTaskforces = string.IsNullOrEmpty(meId)
            ? new List<string>()
            : await db.TaskforceAgenten.Where(ta => ta.AgentId == meId)
                .Select(ta => ta.TaskforceId).Distinct().ToListAsync(cancellationToken);

        // Sichtbarkeit = Empfängerkreis ODER Führung (Aufsicht) ODER Verfasser.
        var rows = await db.Ankuendigungen
            .Where(a => istFuehrung
                || a.ErstelltVonId == meId
                || a.Zielgruppe == AnkuendigungZielgruppe.AlleAktiven
                || (a.Zielgruppe == AnkuendigungZielgruppe.Taskforce && a.ZielId != null && meineTaskforces.Contains(a.ZielId))
                || (a.Zielgruppe == AnkuendigungZielgruppe.TruEinheit && istTRU)
                || (a.Zielgruppe == AnkuendigungZielgruppe.AbDienstgrad && meinDienstgrad != null
                    && a.MinDienstgrad != null && meinDienstgrad >= a.MinDienstgrad))
            .OrderByDescending(a => a.Wichtig)
            .ThenByDescending(a => a.ErstelltAm)
            .Select(a => new
            {
                a.Id, a.Aktenzeichen, a.Titel, a.Inhalt, a.Wichtig, a.Zielgruppe, a.ZielId, a.MinDienstgrad,
                a.AlsBroadcast, a.QuittierungVerlangt, a.ErstelltAm, a.ErstelltVonId,
            })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return new();
        }

        var ids = rows.Select(r => r.Id).ToList();

        // Quittierungs-Zeilen (flach) für Betrachter-Status + Zähler – in-memory aggregiert.
        var quitt = await db.AnkuendigungQuittierungen
            .Where(q => ids.Contains(q.AnkuendigungId))
            .Select(q => new { q.AnkuendigungId, q.AgentId, q.QuittiertAm })
            .ToListAsync(cancellationToken);
        var quittJeId = quitt.GroupBy(q => q.AnkuendigungId).ToDictionary(g => g.Key, g => g.ToList());

        // Ersteller-Codenamen (öffentlich, nie Klarname).
        var erstellerIds = rows.Select(r => r.ErstelltVonId).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        var erstellerNamen = erstellerIds.Count == 0
            ? new Dictionary<string, string>()
            : await db.Users.Where(u => erstellerIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Codename }).ToDictionaryAsync(u => u.Id, u => u.Codename, cancellationToken);

        // Taskforce-Namen für die Zielgruppen-Anzeige.
        var tfIds = rows.Where(r => r.Zielgruppe == AnkuendigungZielgruppe.Taskforce && r.ZielId != null)
            .Select(r => r.ZielId!).Distinct().ToList();
        var tfNamen = tfIds.Count == 0
            ? new Dictionary<string, string>()
            : await db.Taskforces.IgnoreQueryFilters().Where(t => tfIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Name }).ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        return rows.Select(r =>
        {
            var alle = quittJeId.TryGetValue(r.Id, out var qs) ? qs : new();
            var meine = string.IsNullOrEmpty(meId) ? null : alle.FirstOrDefault(x => x.AgentId == meId);
            return new AnkuendigungZeile
            {
                Id = r.Id,
                Aktenzeichen = r.Aktenzeichen,
                Titel = r.Titel,
                Inhalt = r.Inhalt,
                Wichtig = r.Wichtig,
                Zielgruppe = r.Zielgruppe,
                ZielAnzeige = ZielAnzeige(r.Zielgruppe, r.ZielId, r.MinDienstgrad, tfNamen),
                AlsBroadcast = r.AlsBroadcast,
                QuittierungVerlangt = r.QuittierungVerlangt,
                ErstelltAm = r.ErstelltAm,
                ErstellerCodename = r.ErstelltVonId is not null && erstellerNamen.TryGetValue(r.ErstelltVonId, out var cn) ? cn : null,
                MussQuittieren = meine is { QuittiertAm: null },
                SchonQuittiert = meine is { QuittiertAm: not null },
                QuittiertAnzahl = alle.Count(x => x.QuittiertAm != null),
                GesamtAnzahl = alle.Count,
                DarfVerwalten = istFuehrung || r.ErstelltVonId == meId,
            };
        }).ToList();
    }

    public async Task<AnkuendigungAnsicht?> GetDetailAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var a = await db.Ankuendigungen.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (a is null)
        {
            return null;
        }

        var meId = handelnder.GetAgentId();
        var istFuehrung = handelnder.IstFuehrung();
        var darfVerwalten = istFuehrung || a.ErstelltVonId == meId;

        // Sichtbarkeit: Verwalter (Führung/Verfasser) ODER Empfänger der Zielgruppe.
        if (!darfVerwalten
            && !await IstEmpfaengerAsync(db, a, meId, handelnder.IstTRU(), handelnder.GetDienstgrad(), cancellationToken))
        {
            return null;
        }

        // Quittierungen (nur wenn verlangt) – Codename für die Verwalter-Liste, Status für den Aufrufer.
        var alleQuitt = a.QuittierungVerlangt
            ? await db.AnkuendigungQuittierungen.Where(q => q.AnkuendigungId == a.Id)
                .Select(q => new { q.AgentId, q.QuittiertAm, Codename = q.Agent!.Codename })
                .ToListAsync(cancellationToken)
            : new();
        var meine = string.IsNullOrEmpty(meId) ? null : alleQuitt.FirstOrDefault(x => x.AgentId == meId);

        string? erstellerCodename = null;
        if (!string.IsNullOrEmpty(a.ErstelltVonId))
        {
            erstellerCodename = await db.Users.Where(u => u.Id == a.ErstelltVonId)
                .Select(u => u.Codename).FirstOrDefaultAsync(cancellationToken);
        }

        string zielAnzeige;
        if (a.Zielgruppe == AnkuendigungZielgruppe.Taskforce && a.ZielId != null)
        {
            var tfName = await db.Taskforces.IgnoreQueryFilters()
                .Where(t => t.Id == a.ZielId).Select(t => t.Name).FirstOrDefaultAsync(cancellationToken);
            zielAnzeige = tfName != null ? $"Taskforce: {tfName}" : "Taskforce";
        }
        else
        {
            zielAnzeige = ZielAnzeige(a.Zielgruppe, a.ZielId, a.MinDienstgrad, new Dictionary<string, string>());
        }

        var zeile = new AnkuendigungZeile
        {
            Id = a.Id,
            Aktenzeichen = a.Aktenzeichen,
            Titel = a.Titel,
            Inhalt = a.Inhalt,
            Wichtig = a.Wichtig,
            Zielgruppe = a.Zielgruppe,
            ZielAnzeige = zielAnzeige,
            AlsBroadcast = a.AlsBroadcast,
            QuittierungVerlangt = a.QuittierungVerlangt,
            ErstelltAm = a.ErstelltAm,
            ErstellerCodename = erstellerCodename,
            MussQuittieren = meine is { QuittiertAm: null },
            SchonQuittiert = meine is { QuittiertAm: not null },
            QuittiertAnzahl = alleQuitt.Count(x => x.QuittiertAm != null),
            GesamtAnzahl = alleQuitt.Count,
            DarfVerwalten = darfVerwalten,
        };

        return new AnkuendigungAnsicht
        {
            Zeile = zeile,
            // Quittierungsliste nur für Verwalter (offene zuerst, dann nach Codename).
            Quittierungen = darfVerwalten
                ? alleQuitt
                    .OrderBy(x => x.QuittiertAm == null ? 0 : 1)
                    .ThenBy(x => x.Codename)
                    .Select(x => new QuittierungZeile(x.Codename, x.QuittiertAm))
                    .ToList()
                : Array.Empty<QuittierungZeile>(),
        };
    }

    public async Task<List<Ankuendigung>> GetPapierkorbAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Ankuendigungen.IgnoreQueryFilters()
            .Where(a => a.IstGeloescht)
            .OrderByDescending(a => a.GeloeschtAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<Ankuendigung> ErstellenAsync(AnkuendigungEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        // Broadcast-Features (gezielte Zielgruppe, Push, Quittierung) sind der Führung vorbehalten.
        var istBroadcastFeature = eingabe.AlsBroadcast
            || eingabe.Zielgruppe != AnkuendigungZielgruppe.AlleAktiven
            || eingabe.QuittierungVerlangt;
        if (istBroadcastFeature)
        {
            Berechtigung.VerlangeFuehrung(handelnder);
        }

        // Zielgruppen-Parameter validieren.
        if (eingabe.Zielgruppe == AnkuendigungZielgruppe.Taskforce && string.IsNullOrWhiteSpace(eingabe.ZielId))
        {
            throw new InvalidOperationException("Bitte eine Taskforce als Zielgruppe wählen.");
        }
        if (eingabe.Zielgruppe == AnkuendigungZielgruppe.AbDienstgrad && eingabe.MinDienstgrad is null)
        {
            throw new InvalidOperationException("Bitte einen Mindest-Dienstgrad als Zielgruppe wählen.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var ankuendigung = new Ankuendigung
        {
            Aktenzeichen = await aktenzeichen.NaechstesAsync(db, "N", cancellationToken),
            Titel = eingabe.Titel.Trim(),
            Inhalt = eingabe.Inhalt?.Trim() ?? string.Empty,
            Wichtig = eingabe.Wichtig,
            Zielgruppe = eingabe.Zielgruppe,
            ZielId = eingabe.Zielgruppe == AnkuendigungZielgruppe.Taskforce ? eingabe.ZielId : null,
            MinDienstgrad = eingabe.Zielgruppe == AnkuendigungZielgruppe.AbDienstgrad ? eingabe.MinDienstgrad : null,
            AlsBroadcast = eingabe.AlsBroadcast,
            QuittierungVerlangt = eingabe.QuittierungVerlangt,
        };
        db.Ankuendigungen.Add(ankuendigung);
        await db.SaveChangesAsync(cancellationToken);

        // Empfängerkreis nur ermitteln, wenn er gebraucht wird (Quittierung-Snapshot und/oder Push).
        var erstellerId = handelnder.GetAgentId();
        var empfaenger = ankuendigung.QuittierungVerlangt || ankuendigung.AlsBroadcast
            ? await EmpfaengerIdsAsync(db, ankuendigung, cancellationToken)
            : new List<string>();

        if (ankuendigung.QuittierungVerlangt)
        {
            // Snapshot der quittierungspflichtigen Empfänger (ohne den Verfasser selbst).
            foreach (var eid in empfaenger.Distinct().Where(x => x != erstellerId))
            {
                db.AnkuendigungQuittierungen.Add(new AnkuendigungQuittierung
                {
                    AnkuendigungId = ankuendigung.Id,
                    AgentId = eid,
                });
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        // Glocken-Broadcast nach dem Commit (Verfasser ausgeschlossen, best-effort).
        if (ankuendigung.AlsBroadcast)
        {
            await notifications.BenachrichtigeVieleAsync(empfaenger, NotificationTyp.Ankuendigung,
                $"Neue Ankündigung: „{ankuendigung.Titel}“.", $"/brett/{ankuendigung.Id}", erstellerId, cancellationToken);
        }

        return ankuendigung;
    }

    public async Task AktualisierenAsync(string id, AnkuendigungEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var a = await db.Ankuendigungen.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Ankündigung '{id}' nicht gefunden.");
        VerlangeErstellerOderFuehrung(a, handelnder);

        // Bewusst nur Inhaltliches editierbar – Zielgruppe/Push/Quittierung sind nach dem Anlegen fix
        // (kein Re-Snapshot/Re-Push beim Bearbeiten).
        a.Titel = eingabe.Titel.Trim();
        a.Inhalt = eingabe.Inhalt?.Trim() ?? string.Empty;
        a.Wichtig = eingabe.Wichtig;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var a = await db.Ankuendigungen.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Ankündigung '{id}' nicht gefunden.");
        VerlangeErstellerOderFuehrung(a, handelnder);
        db.Ankuendigungen.Remove(a); // Interceptor wandelt das in Soft-Delete.
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var a = await db.Ankuendigungen.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Ankündigung '{id}' nicht gefunden.");

        a.IstGeloescht = false;
        a.GeloeschtAm = null;
        a.GeloeschtVonId = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task QuittierenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        var meId = handelnder.GetAgentId();
        if (string.IsNullOrEmpty(meId))
        {
            throw new UnauthorizedAccessException("Kein angemeldeter Agent.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var zeile = await db.AnkuendigungQuittierungen
            .FirstOrDefaultAsync(q => q.AnkuendigungId == id && q.AgentId == meId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Diese Ankündigung erfordert keine Quittierung von dir.");

        if (zeile.QuittiertAm is not null)
        {
            return; // bereits quittiert – idempotent
        }
        zeile.QuittiertAm = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetOffeneQuittierungenAnzahlAsync(ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        var meId = handelnder.GetAgentId();
        if (string.IsNullOrEmpty(meId))
        {
            return 0;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Referenz-Navigation erzwingt den Join auf die Ankündigung → deren Soft-Delete-Filter blendet
        // Papierkorb-Ankündigungen automatisch aus (keine „Geister"-Quittierungen).
        return await db.AnkuendigungQuittierungen
            .CountAsync(q => q.AgentId == meId && q.QuittiertAm == null && q.Ankuendigung!.QuittierungVerlangt, cancellationToken);
    }

    // ---- Helfer ----

    /// <summary>Aktive Agent-Ids des Empfängerkreises einer Ankündigung (für Snapshot/Push).</summary>
    private static async Task<List<string>> EmpfaengerIdsAsync(AppDbContext db, Ankuendigung a, CancellationToken cancellationToken)
    {
        var query = db.Users.Where(u => u.Status == AgentStatus.Aktiv);
        query = a.Zielgruppe switch
        {
            AnkuendigungZielgruppe.TruEinheit => query.Where(u => u.IstTRU),
            AnkuendigungZielgruppe.AbDienstgrad => query.Where(u => u.Dienstgrad != null && u.Dienstgrad >= a.MinDienstgrad),
            AnkuendigungZielgruppe.Taskforce => query.Where(u => db.TaskforceAgenten.Any(ta => ta.TaskforceId == a.ZielId && ta.AgentId == u.Id)),
            _ => query, // AlleAktiven
        };
        return await query.Select(u => u.Id).ToListAsync(cancellationToken);
    }

    /// <summary>Prüft, ob der Aufrufer zum Empfängerkreis einer Ankündigung gehört (Brett-/Detail-Sichtbarkeit).</summary>
    private static async Task<bool> IstEmpfaengerAsync(AppDbContext db, Ankuendigung a, string? meId, bool istTRU,
        Dienstgrad? meinDienstgrad, CancellationToken cancellationToken)
    {
        switch (a.Zielgruppe)
        {
            case AnkuendigungZielgruppe.AlleAktiven:
                return true;
            case AnkuendigungZielgruppe.TruEinheit:
                return istTRU;
            case AnkuendigungZielgruppe.AbDienstgrad:
                return meinDienstgrad != null && a.MinDienstgrad != null && meinDienstgrad >= a.MinDienstgrad;
            case AnkuendigungZielgruppe.Taskforce:
                return a.ZielId != null && !string.IsNullOrEmpty(meId)
                    && await db.TaskforceAgenten.AnyAsync(ta => ta.TaskforceId == a.ZielId && ta.AgentId == meId, cancellationToken);
            default:
                return false;
        }
    }

    private static string ZielAnzeige(AnkuendigungZielgruppe zielgruppe, string? zielId, Dienstgrad? minDienstgrad,
        IReadOnlyDictionary<string, string> taskforceNamen) => zielgruppe switch
    {
        AnkuendigungZielgruppe.AlleAktiven => "Alle aktiven Agenten",
        AnkuendigungZielgruppe.Taskforce => zielId != null && taskforceNamen.TryGetValue(zielId, out var n)
            ? $"Taskforce: {n}" : "Taskforce",
        AnkuendigungZielgruppe.TruEinheit => "TRU-Einheit",
        AnkuendigungZielgruppe.AbDienstgrad => $"Ab {DienstgradAnzeige.Name(minDienstgrad)}",
        _ => "—",
    };

    private static void VerlangeErstellerOderFuehrung(Ankuendigung a, ClaimsPrincipal handelnder)
    {
        if (handelnder.IstFuehrung())
        {
            return;
        }
        var meId = handelnder.GetAgentId();
        if (!string.IsNullOrEmpty(meId) && a.ErstelltVonId == meId)
        {
            return;
        }
        throw new UnauthorizedAccessException("Diese Ankündigung darf nur ihr Verfasser oder die Führung bearbeiten.");
    }
}
