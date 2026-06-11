using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Aufgaben;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Aufgaben;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IAufgabeService" />
public class AufgabeService(
    IDbContextFactory<AppDbContext> dbFactory,
    IAktenzeichenService aktenzeichen,
    INotificationService notifications) : IAufgabeService
{
    public async Task<List<AufgabeZeile>> GetTeamboardAsync(bool nurMeine, ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var meId = handelnder.GetAgentId();
        var istFuehrung = handelnder.IstFuehrung();
        var query = db.Aufgaben.AsQueryable();
        if (nurMeine && !string.IsNullOrEmpty(meId))
        {
            // „Meine" = selbst angelegt ODER zugewiesen (korreliertes EXISTS – auf MySQL/MariaDB zulässig).
            query = query.Where(a => a.ErstelltVonId == meId
                || db.AufgabeZuweisungen.Any(z => z.AufgabeId == a.Id && z.AgentId == meId));
        }

        // Aufgaben flach laden (kein Collection-Projektions-Subselect – Pomelo-Regel).
        var rows = await query
            .OrderByDescending(a => a.GeaendertAm ?? a.ErstelltAm)
            .Select(a => new
            {
                a.Id, a.Aktenzeichen, a.Titel, a.Status, a.Prioritaet, a.Faelligkeit, a.ErledigtAm, a.ErstelltVonId,
            })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return new();
        }

        var ids = rows.Select(r => r.Id).ToList();
        // Zuweisungen flach über WHERE FK IN; Codename per Referenz-Join (kein LATERAL).
        var zuweisungen = await db.AufgabeZuweisungen
            .Where(z => ids.Contains(z.AufgabeId))
            .Select(z => new { z.AufgabeId, z.AgentId, Codename = z.Agent!.Codename })
            .ToListAsync(cancellationToken);
        var zugewieseneJeAufgabe = zuweisungen
            .GroupBy(z => z.AufgabeId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Codename).OrderBy(c => c).ToList());
        // Wer ist mir zugewiesen? (für die Status-Ändern-Berechtigung des Kanban-Boards)
        var meineZuweisungen = string.IsNullOrEmpty(meId)
            ? new HashSet<string>()
            : zuweisungen.Where(z => z.AgentId == meId).Select(z => z.AufgabeId).ToHashSet();

        // Ersteller-Codenamen einsammeln (Codename ist öffentlich, nie Klarname).
        var erstellerIds = rows.Select(r => r.ErstelltVonId).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        var erstellerNamen = await db.Users
            .Where(u => erstellerIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Codename })
            .ToDictionaryAsync(u => u.Id, u => u.Codename, cancellationToken);

        return rows.Select(r => new AufgabeZeile
        {
            Id = r.Id,
            Aktenzeichen = r.Aktenzeichen,
            Titel = r.Titel,
            Status = r.Status,
            Prioritaet = r.Prioritaet,
            Faelligkeit = r.Faelligkeit,
            ErledigtAm = r.ErledigtAm,
            ErstellerCodename = r.ErstelltVonId is not null && erstellerNamen.TryGetValue(r.ErstelltVonId, out var name) ? name : null,
            ZugewieseneCodenames = zugewieseneJeAufgabe.TryGetValue(r.Id, out var liste) ? liste : new List<string>(),
            // Status ändern dürfen Führung, Ersteller oder zugewiesene Agenten (spiegelt StatusSetzenAsync).
            // Die Nur-Lese-Aufsicht darf nichts ändern – auch keine eigenen/zugewiesenen Aufgaben.
            DarfStatusAendern = handelnder.DarfSchreiben() && (istFuehrung || r.ErstelltVonId == meId || meineZuweisungen.Contains(r.Id)),
        }).ToList();
    }

    public async Task<Aufgabe?> GetDetailAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Aufgaben.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<List<Aufgabe>> GetPapierkorbAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Aufgaben.IgnoreQueryFilters()
            .Where(a => a.IstGeloescht)
            .OrderByDescending(a => a.GeloeschtAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Aufgabe>> SucheAsync(string? suchtext, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Aufgaben.AsQueryable();

        var s = suchtext?.Trim();
        if (!string.IsNullOrEmpty(s))
        {
            query = query.Where(a => a.Titel.Contains(s) || a.Aktenzeichen.Contains(s));
        }

        return await query
            .OrderBy(a => a.Titel)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    public async Task<Aufgabe> ErstellenAsync(AufgabeEingabe eingabe, IReadOnlyList<string> agentIds,
        ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var aufgabe = new Aufgabe
        {
            Aktenzeichen = await aktenzeichen.NaechstesAsync(db, "A", cancellationToken),
            Titel = eingabe.Titel.Trim(),
            Beschreibung = eingabe.Beschreibung.TrimToNull(),
            Status = eingabe.Status,
            Prioritaet = eingabe.Prioritaet,
            Faelligkeit = eingabe.Faelligkeit,
            ErledigtAm = AufgabeStatusAnzeige.IstAbgeschlossen(eingabe.Status) ? DateTime.UtcNow : null,
        };
        db.Aufgaben.Add(aufgabe);
        await db.SaveChangesAsync(cancellationToken);

        // Nur tatsächlich existierende, aktive Agenten zuweisen (dedupliziert).
        var gueltige = agentIds.Count == 0
            ? new List<string>()
            : await db.Users
                .Where(u => agentIds.Contains(u.Id) && u.Status == AgentStatus.Aktiv)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
        foreach (var agentId in gueltige.Distinct())
        {
            db.AufgabeZuweisungen.Add(new AufgabeZuweisung { AufgabeId = aufgabe.Id, AgentId = agentId });
        }
        if (gueltige.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        // Nach dem Commit benachrichtigen (der Ersteller selbst bekommt keine Meldung).
        var erstellerId = handelnder.GetAgentId();
        foreach (var agentId in gueltige.Distinct().Where(x => x != erstellerId))
        {
            await notifications.BenachrichtigeAsync(agentId, NotificationTyp.AufgabeZugewiesen,
                $"Dir wurde eine Aufgabe zugewiesen: „{aufgabe.Titel}“.", $"/aufgaben/{aufgabe.Id}", cancellationToken);
        }

        return aufgabe;
    }

    public async Task AktualisierenAsync(string id, AufgabeEingabe eingabe, ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var aufgabe = await db.Aufgaben.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Aufgabe '{id}' nicht gefunden.");
        VerlangeErstellerOderFuehrung(aufgabe, handelnder);

        var alterStatus = aufgabe.Status;
        aufgabe.Titel = eingabe.Titel.Trim();
        aufgabe.Beschreibung = eingabe.Beschreibung.TrimToNull();
        aufgabe.Prioritaet = eingabe.Prioritaet;
        aufgabe.Faelligkeit = eingabe.Faelligkeit;
        SetzeStatus(aufgabe, eingabe.Status);
        await db.SaveChangesAsync(cancellationToken);

        await BenachrichtigeErstellerBeiErledigtAsync(aufgabe, alterStatus, handelnder, cancellationToken);
    }

    public async Task StatusSetzenAsync(string id, AufgabeStatus status, ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var aufgabe = await db.Aufgaben.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Aufgabe '{id}' nicht gefunden.");
        await VerlangeBeteiligtOderFuehrungAsync(db, aufgabe, handelnder, cancellationToken);

        var alterStatus = aufgabe.Status;
        SetzeStatus(aufgabe, status);
        await db.SaveChangesAsync(cancellationToken);

        await BenachrichtigeErstellerBeiErledigtAsync(aufgabe, alterStatus, handelnder, cancellationToken);
    }

    public async Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var aufgabe = await db.Aufgaben.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Aufgabe '{id}' nicht gefunden.");
        VerlangeErstellerOderFuehrung(aufgabe, handelnder);
        db.Aufgaben.Remove(aufgabe);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var aufgabe = await db.Aufgaben.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Aufgabe '{id}' nicht gefunden.");

        aufgabe.IstGeloescht = false;
        aufgabe.GeloeschtAm = null;
        aufgabe.GeloeschtVonId = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<AufgabeZuweisung>> GetZuweisungenAsync(string aufgabeId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AufgabeZuweisungen
            .Where(z => z.AufgabeId == aufgabeId)
            .Include(z => z.Agent)
            .OrderBy(z => z.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task AgentZuweisenAsync(string aufgabeId, string agentId, ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var aufgabe = await db.Aufgaben.FirstOrDefaultAsync(a => a.Id == aufgabeId, cancellationToken)
            ?? throw new InvalidOperationException($"Aufgabe '{aufgabeId}' nicht gefunden.");
        VerlangeErstellerOderFuehrung(aufgabe, handelnder);

        if (!await db.Users.AnyAsync(u => u.Id == agentId && u.Status == AgentStatus.Aktiv, cancellationToken))
        {
            throw new InvalidOperationException("Der gewählte Agent wurde nicht gefunden oder ist nicht aktiv.");
        }
        if (await db.AufgabeZuweisungen.AnyAsync(z => z.AufgabeId == aufgabeId && z.AgentId == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Dieser Agent ist der Aufgabe bereits zugewiesen.");
        }

        db.AufgabeZuweisungen.Add(new AufgabeZuweisung { AufgabeId = aufgabeId, AgentId = agentId });
        await db.SaveChangesAsync(cancellationToken);

        if (agentId != handelnder.GetAgentId())
        {
            await notifications.BenachrichtigeAsync(agentId, NotificationTyp.AufgabeZugewiesen,
                $"Dir wurde eine Aufgabe zugewiesen: „{aufgabe.Titel}“.", $"/aufgaben/{aufgabe.Id}", cancellationToken);
        }
    }

    public async Task AgentEntfernenAsync(string zuweisungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var zuweisung = await db.AufgabeZuweisungen
            .Include(z => z.Aufgabe)
            .FirstOrDefaultAsync(z => z.Id == zuweisungId, cancellationToken);
        if (zuweisung is null)
        {
            return;
        }
        if (zuweisung.Aufgabe is not null)
        {
            VerlangeErstellerOderFuehrung(zuweisung.Aufgabe, handelnder);
        }
        db.AufgabeZuweisungen.Remove(zuweisung);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<AuditLog>> GetHistorieAsync(string aufgabeId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var zuweisungIds = await db.AufgabeZuweisungen
            .Where(z => z.AufgabeId == aufgabeId)
            .Select(z => z.Id)
            .ToListAsync(cancellationToken);

        // Manuelle Verknüpfungen, die diese Aufgabe als Quelle oder Ziel berühren (inkl. entfernter, für „entfernt"-Einträge).
        var beziehungIds = await db.Verknuepfungen
            .IgnoreQueryFilters()
            .Where(v => !v.Automatisch
                && ((v.VonTyp == nameof(Aufgabe) && v.VonId == aufgabeId)
                 || (v.NachTyp == nameof(Aufgabe) && v.NachId == aufgabeId)))
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);

        var ids = new HashSet<string> { aufgabeId };
        ids.UnionWith(zuweisungIds);
        ids.UnionWith(beziehungIds);
        var typen = new[] { nameof(Aufgabe), nameof(AufgabeZuweisung), nameof(Verknuepfung) };

        return await db.AuditLogs
            .Where(a => typen.Contains(a.EntitaetTyp) && ids.Contains(a.EntitaetId))
            .OrderByDescending(a => a.Zeitpunkt)
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> BezugAnzeigeAsync(string entitaetTyp, string entitaetId, ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entitaetTyp) || string.IsNullOrWhiteSpace(entitaetId))
        {
            return null;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Nur anzeigen, was der Aufrufer sehen darf (Verschlusssache/Papierkorb/Personalakte/Taskforce-Gate).
        // VS: die Nur-Lese-Aufsicht darf einsehen (DarfVerschlusssacheLesen); Taskforces: nur wenn zugeteilt (meId).
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, entitaetTyp, entitaetId, handelnder.DarfVerschlusssacheLesen(), cancellationToken, handelnder.GetAgentId()))
        {
            return null;
        }
        var map = await AktenReferenz.AufloesenAsync(db, new[] { (entitaetTyp, entitaetId) }, cancellationToken,
            darfAlleTaskforces: handelnder.DarfAlleTaskforcesSehen(), meId: handelnder.GetAgentId());
        return map.TryGetValue((entitaetTyp, entitaetId), out var a) ? a.Anzeige : null;
    }

    // ---- Helfer ----

    /// <summary>Setzt den Status und pflegt den Erledigt-Zeitpunkt (setzen bei Abschluss, leeren bei erneut offen).</summary>
    private static void SetzeStatus(Aufgabe aufgabe, AufgabeStatus neu)
    {
        var warAbgeschlossen = AufgabeStatusAnzeige.IstAbgeschlossen(aufgabe.Status);
        var istAbgeschlossen = AufgabeStatusAnzeige.IstAbgeschlossen(neu);
        aufgabe.Status = neu;
        if (istAbgeschlossen && !warAbgeschlossen)
        {
            aufgabe.ErledigtAm = DateTime.UtcNow;
        }
        else if (!istAbgeschlossen)
        {
            aufgabe.ErledigtAm = null;
        }
    }

    /// <summary>Benachrichtigt den Ersteller, wenn die Aufgabe gerade erst auf „Erledigt" gesetzt wurde (und er nicht selbst handelt).</summary>
    private async Task BenachrichtigeErstellerBeiErledigtAsync(Aufgabe aufgabe, AufgabeStatus alterStatus,
        ClaimsPrincipal handelnder, CancellationToken cancellationToken)
    {
        if (aufgabe.Status != AufgabeStatus.Erledigt || alterStatus == AufgabeStatus.Erledigt)
        {
            return;
        }
        var erstellerId = aufgabe.ErstelltVonId;
        if (!string.IsNullOrEmpty(erstellerId) && erstellerId != handelnder.GetAgentId())
        {
            await notifications.BenachrichtigeAsync(erstellerId, NotificationTyp.AufgabeZugewiesen,
                $"Aufgabe erledigt: „{aufgabe.Titel}“.", $"/aufgaben/{aufgabe.Id}", cancellationToken);
        }
    }

    private static void VerlangeErstellerOderFuehrung(Aufgabe aufgabe, ClaimsPrincipal handelnder)
    {
        if (handelnder.IstFuehrung())
        {
            return;
        }
        var meId = handelnder.GetAgentId();
        if (!string.IsNullOrEmpty(meId) && aufgabe.ErstelltVonId == meId)
        {
            return;
        }
        throw new UnauthorizedAccessException("Diese Aufgabe darf nur ihr Ersteller oder die Führung bearbeiten.");
    }

    private static async Task VerlangeBeteiligtOderFuehrungAsync(AppDbContext db, Aufgabe aufgabe,
        ClaimsPrincipal handelnder, CancellationToken cancellationToken)
    {
        if (handelnder.IstFuehrung())
        {
            return;
        }
        var meId = handelnder.GetAgentId();
        if (!string.IsNullOrEmpty(meId)
            && (aufgabe.ErstelltVonId == meId
                || await db.AufgabeZuweisungen.AnyAsync(z => z.AufgabeId == aufgabe.Id && z.AgentId == meId, cancellationToken)))
        {
            return;
        }
        throw new UnauthorizedAccessException("Den Status darf nur ein Beteiligter (Ersteller/Zugewiesener) oder die Führung ändern.");
    }
}
