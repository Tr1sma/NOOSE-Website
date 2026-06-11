using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IWiedervorlageService" />
public class WiedervorlageService(IDbContextFactory<AppDbContext> dbFactory) : IWiedervorlageService
{
    public async Task<List<WiedervorlageItem>> GetFuerAkteAsync(string entitaetTyp, string entitaetId,
        ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Wiedervorlagen einer Akte nur zeigen, wenn der Aufrufer die Akte sehen darf (VS/Papierkorb-Gate).
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, entitaetTyp, entitaetId, handelnder.IstFuehrung(), cancellationToken))
        {
            return new();
        }

        var rows = await db.Wiedervorlagen
            .Where(w => w.EntitaetTyp == entitaetTyp && w.EntitaetId == entitaetId)
            .OrderBy(w => w.Erledigt).ThenBy(w => w.FaelligAm)
            .Select(w => new
            {
                w.Id, w.FaelligAm, w.Notiz, w.ZustaendigerAgentId, w.Erledigt, w.ErledigtAm, w.ErstelltVonId,
            })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return new();
        }

        // Zuständigen-Codenamen einsammeln (Codename ist öffentlich, nie Klarname).
        var agentIds = rows.Select(r => r.ZustaendigerAgentId).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList()!;
        var codenamen = agentIds.Count == 0
            ? new Dictionary<string, string?>()
            : (await db.Users.Where(u => agentIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Codename })
                .ToListAsync(cancellationToken))
                .ToDictionary(u => u.Id, u => (string?)u.Codename);

        var meId = handelnder.GetAgentId();
        var istFuehrung = handelnder.IstFuehrung();
        var jetzt = DateTime.UtcNow;

        return rows.Select(r => new WiedervorlageItem(
            Id: r.Id,
            FaelligAm: r.FaelligAm,
            Notiz: r.Notiz,
            ZustaendigerAgentId: r.ZustaendigerAgentId,
            ZustaendigerCodename: r.ZustaendigerAgentId is not null && codenamen.TryGetValue(r.ZustaendigerAgentId, out var cn) ? cn : null,
            Erledigt: r.Erledigt,
            ErledigtAm: r.ErledigtAm,
            Ueberfaellig: !r.Erledigt && r.FaelligAm <= jetzt,
            DarfBearbeiten: istFuehrung || r.ErstelltVonId == meId || r.ZustaendigerAgentId == meId)).ToList();
    }

    public async Task ErstellenAsync(string entitaetTyp, string entitaetId, WiedervorlageEingabe eingabe,
        ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, entitaetTyp, entitaetId, handelnder.IstFuehrung(), cancellationToken))
        {
            throw new UnauthorizedAccessException("Für diese Akte darf keine Wiedervorlage angelegt werden.");
        }

        var zustaendigerId = await BestimmeZustaendigenAsync(db, eingabe.ZustaendigerAgentId, handelnder, cancellationToken);

        db.Wiedervorlagen.Add(new Wiedervorlage
        {
            EntitaetTyp = entitaetTyp,
            EntitaetId = entitaetId,
            FaelligAm = eingabe.FaelligAm.ToUniversalTime(),
            Notiz = eingabe.Notiz.TrimToNull(),
            ZustaendigerAgentId = zustaendigerId,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AktualisierenAsync(string id, WiedervorlageEingabe eingabe, ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var w = await db.Wiedervorlagen.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Wiedervorlage nicht gefunden.");
        VerlangeBearbeiten(w, handelnder);

        var neuFaellig = eingabe.FaelligAm.ToUniversalTime();
        // Bei verschobenem Termin erneut benachrichtigen lassen (Dedupe-Stempel zurücksetzen).
        if (w.FaelligAm != neuFaellig)
        {
            w.BenachrichtigtAm = null;
        }
        w.FaelligAm = neuFaellig;
        w.Notiz = eingabe.Notiz.TrimToNull();
        w.ZustaendigerAgentId = await BestimmeZustaendigenAsync(db, eingabe.ZustaendigerAgentId, handelnder, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ErledigenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var w = await db.Wiedervorlagen.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Wiedervorlage nicht gefunden.");
        VerlangeBearbeiten(w, handelnder);

        if (!w.Erledigt)
        {
            w.Erledigt = true;
            w.ErledigtAm = DateTime.UtcNow;
            w.ErledigtVonId = handelnder.GetAgentId();
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task WiedereroeffnenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var w = await db.Wiedervorlagen.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Wiedervorlage nicht gefunden.");
        VerlangeBearbeiten(w, handelnder);

        if (w.Erledigt)
        {
            w.Erledigt = false;
            w.ErledigtAm = null;
            w.ErledigtVonId = null;
            // Erneut fällig → darf wieder gemeldet werden.
            w.BenachrichtigtAm = null;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var w = await db.Wiedervorlagen.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Wiedervorlage nicht gefunden.");
        VerlangeLoeschen(w, handelnder);
        db.Wiedervorlagen.Remove(w);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<WiedervorlageDashboardItem>> GetMeineFaelligenAsync(ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default)
    {
        var meId = handelnder.GetAgentId();
        if (string.IsNullOrEmpty(meId))
        {
            return new();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var jetzt = DateTime.UtcNow;

        // Offen + fällig, und ich bin zuständig ODER folge der Akte (korreliertes EXISTS – auf MySQL/MariaDB zulässig).
        var rows = await db.Wiedervorlagen
            .Where(w => !w.Erledigt && w.FaelligAm <= jetzt
                && (w.ZustaendigerAgentId == meId
                    || db.Watchlisten.Any(x => x.AgentId == meId && x.EntitaetTyp == w.EntitaetTyp && x.EntitaetId == w.EntitaetId)))
            .OrderBy(w => w.FaelligAm)
            .Select(w => new { w.Id, w.EntitaetTyp, w.EntitaetId, w.FaelligAm, w.Notiz })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return new();
        }

        // Akten-Namen + Href in einer Sammelabfrage; aus Sicht des Aufrufers VS-/Papierkorb-gefiltert.
        var istFuehrung = handelnder.IstFuehrung();
        var refs = rows.Select(r => (r.EntitaetTyp, r.EntitaetId)).Distinct().ToList();
        var aufgeloest = await AktenReferenz.AufloesenAsync(db, refs, cancellationToken);

        var ergebnis = new List<WiedervorlageDashboardItem>();
        foreach (var r in rows)
        {
            if (!aufgeloest.TryGetValue((r.EntitaetTyp, r.EntitaetId), out var a))
            {
                continue; // Akte im Papierkorb/unbekannt → überspringen.
            }
            if (a.Verschluss && !istFuehrung)
            {
                continue; // Verschlusssache für Nicht-Führung verbergen.
            }
            ergebnis.Add(new WiedervorlageDashboardItem(r.Id, a.Anzeige, a.Href, r.FaelligAm, r.Notiz));
        }
        return ergebnis;
    }

    // ---- Helfer ----

    private static async Task<string?> BestimmeZustaendigenAsync(AppDbContext db, string? gewuenscht,
        ClaimsPrincipal handelnder, CancellationToken cancellationToken)
    {
        // Ohne Auswahl: der Ersteller wird zuständig.
        if (string.IsNullOrWhiteSpace(gewuenscht))
        {
            return handelnder.GetAgentId();
        }
        // Mit Auswahl: muss ein existierender, aktiver Agent sein.
        var gueltig = await db.Users.AnyAsync(u => u.Id == gewuenscht && u.Status == AgentStatus.Aktiv, cancellationToken);
        if (!gueltig)
        {
            throw new InvalidOperationException("Der gewählte zuständige Agent wurde nicht gefunden oder ist nicht aktiv.");
        }
        return gewuenscht;
    }

    private static void VerlangeBearbeiten(Wiedervorlage w, ClaimsPrincipal handelnder)
    {
        if (handelnder.IstFuehrung())
        {
            return;
        }
        var meId = handelnder.GetAgentId();
        if (!string.IsNullOrEmpty(meId) && (w.ErstelltVonId == meId || w.ZustaendigerAgentId == meId))
        {
            return;
        }
        throw new UnauthorizedAccessException("Diese Wiedervorlage darf nur ihr Ersteller, der Zuständige oder die Führung bearbeiten.");
    }

    private static void VerlangeLoeschen(Wiedervorlage w, ClaimsPrincipal handelnder)
    {
        if (handelnder.IstFuehrung())
        {
            return;
        }
        var meId = handelnder.GetAgentId();
        if (!string.IsNullOrEmpty(meId) && w.ErstelltVonId == meId)
        {
            return;
        }
        throw new UnauthorizedAccessException("Eine Wiedervorlage darf nur ihr Ersteller oder die Führung löschen.");
    }
}
