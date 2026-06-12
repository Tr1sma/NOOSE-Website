using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Termine;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Termine;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ITerminService" />
public class TerminService(
    IDbContextFactory<AppDbContext> dbFactory,
    IAktenzeichenService aktenzeichen,
    INotificationService notifications) : ITerminService
{
    public async Task<Termin?> GetDetailAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Eingeschränkte Termine sind nur für Beteiligte/Aufsicht zugänglich (null = „nicht gefunden/zugänglich").
        return await db.Termine
            .NurSichtbare(db, handelnder.DarfVerschlusssacheLesen(), handelnder.GetAgentId())
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<List<Termin>> GetPapierkorbAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Termine.IgnoreQueryFilters()
            .Where(t => t.IstGeloescht)
            .OrderByDescending(t => t.GeloeschtAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Termin>> SucheAsync(string? suchtext, bool darfAlles, string? meId, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Eingeschränkte Termine tauchen im Picker nur für Beteiligte/Aufsicht auf.
        var query = db.Termine.NurSichtbare(db, darfAlles, meId);

        var s = suchtext?.Trim();
        if (!string.IsNullOrEmpty(s))
        {
            query = query.Where(t => t.Titel.Contains(s) || t.Aktenzeichen.Contains(s));
        }

        return await query
            .OrderByDescending(t => t.Beginn)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    public async Task<Termin> ErstellenAsync(TerminEingabe eingabe, IReadOnlyList<string> agentIds,
        ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        var (beginnUtc, endeUtc) = ZeitenAusEingabe(eingabe);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var termin = new Termin
        {
            Aktenzeichen = await aktenzeichen.NaechstesAsync(db, "TM", cancellationToken),
            Titel = eingabe.Titel.Trim(),
            Kategorie = eingabe.Kategorie,
            Status = eingabe.Status,
            Ort = eingabe.Ort.TrimToNull(),
            Beginn = beginnUtc,
            Ende = endeUtc,
            Ganztaegig = eingabe.Ganztaegig,
            Beschreibung = eingabe.Beschreibung.TrimToNull(),
            Sichtbarkeit = eingabe.Sichtbarkeit,
        };
        db.Termine.Add(termin);
        await db.SaveChangesAsync(cancellationToken);

        // Nur tatsächlich existierende, aktive Agenten zuteilen (dedupliziert).
        var gueltige = agentIds.Count == 0
            ? new List<string>()
            : await db.Users
                .Where(u => agentIds.Contains(u.Id) && u.Status == AgentStatus.Aktiv)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
        foreach (var agentId in gueltige.Distinct())
        {
            db.TerminZuweisungen.Add(new TerminZuweisung { TerminId = termin.Id, AgentId = agentId });
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
            await notifications.BenachrichtigeAsync(agentId, NotificationTyp.TerminZugewiesen,
                $"Du bist als Teilnehmer eingetragen: „{termin.Titel}“.", $"/kalender/{termin.Id}", cancellationToken);
        }

        return termin;
    }

    public async Task AktualisierenAsync(string id, TerminEingabe eingabe, ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default)
    {
        var (beginnUtc, endeUtc) = ZeitenAusEingabe(eingabe);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var termin = await db.Termine.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Termin '{id}' nicht gefunden.");
        VerlangeErstellerOderFuehrung(termin, handelnder);

        termin.Titel = eingabe.Titel.Trim();
        termin.Kategorie = eingabe.Kategorie;
        termin.Status = eingabe.Status;
        termin.Ort = eingabe.Ort.TrimToNull();
        termin.Beginn = beginnUtc;
        termin.Ende = endeUtc;
        termin.Ganztaegig = eingabe.Ganztaegig;
        termin.Beschreibung = eingabe.Beschreibung.TrimToNull();
        termin.Sichtbarkeit = eingabe.Sichtbarkeit;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var termin = await db.Termine.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Termin '{id}' nicht gefunden.");
        VerlangeErstellerOderFuehrung(termin, handelnder);
        db.Termine.Remove(termin);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var termin = await db.Termine.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Termin '{id}' nicht gefunden.");

        termin.IstGeloescht = false;
        termin.GeloeschtAm = null;
        termin.GeloeschtVonId = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<TerminZuweisung>> GetTeilnehmerAsync(string terminId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.TerminZuweisungen
            .Where(z => z.TerminId == terminId)
            .Include(z => z.Agent)
            .OrderBy(z => z.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task AgentZuweisenAsync(string terminId, string agentId, ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var termin = await db.Termine.FirstOrDefaultAsync(t => t.Id == terminId, cancellationToken)
            ?? throw new InvalidOperationException($"Termin '{terminId}' nicht gefunden.");
        VerlangeErstellerOderFuehrung(termin, handelnder);

        if (!await db.Users.AnyAsync(u => u.Id == agentId && u.Status == AgentStatus.Aktiv, cancellationToken))
        {
            throw new InvalidOperationException("Der gewählte Agent wurde nicht gefunden oder ist nicht aktiv.");
        }
        if (await db.TerminZuweisungen.AnyAsync(z => z.TerminId == terminId && z.AgentId == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Dieser Agent ist dem Termin bereits zugeteilt.");
        }

        db.TerminZuweisungen.Add(new TerminZuweisung { TerminId = terminId, AgentId = agentId });
        await db.SaveChangesAsync(cancellationToken);

        if (agentId != handelnder.GetAgentId())
        {
            await notifications.BenachrichtigeAsync(agentId, NotificationTyp.TerminZugewiesen,
                $"Du bist als Teilnehmer eingetragen: „{termin.Titel}“.", $"/kalender/{termin.Id}", cancellationToken);
        }
    }

    public async Task AgentEntfernenAsync(string zuweisungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var zuweisung = await db.TerminZuweisungen
            .Include(z => z.Termin)
            .FirstOrDefaultAsync(z => z.Id == zuweisungId, cancellationToken);
        if (zuweisung is null)
        {
            return;
        }
        if (zuweisung.Termin is not null)
        {
            VerlangeErstellerOderFuehrung(zuweisung.Termin, handelnder);
        }
        db.TerminZuweisungen.Remove(zuweisung);
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---- Helfer ----

    /// <summary>Lokale RP-Zeit (wie eingegeben) → UTC für die Speicherung. Behandelt unspezifizierte Kinds als lokal.</summary>
    private static DateTime LokalNachUtc(DateTime lokal)
        => DateTime.SpecifyKind(lokal, DateTimeKind.Local).ToUniversalTime();

    /// <summary>Prüft/normalisiert Beginn &amp; Ende der Eingabe und liefert sie als UTC. Ganztägig → auf das reine
    /// Datum (00:00) normalisiert; das Ende darf nicht vor dem Beginn liegen.</summary>
    private static (DateTime Beginn, DateTime? Ende) ZeitenAusEingabe(TerminEingabe eingabe)
    {
        if (eingabe.Beginn is null)
        {
            throw new InvalidOperationException("Ein Termin braucht einen Beginn.");
        }
        var beginnLokal = eingabe.Ganztaegig ? eingabe.Beginn.Value.Date : eingabe.Beginn.Value;
        DateTime? endeLokal = eingabe.Ende is { } e ? (eingabe.Ganztaegig ? e.Date : e) : null;
        if (endeLokal is { } el && el < beginnLokal)
        {
            throw new InvalidOperationException("Das Ende darf nicht vor dem Beginn liegen.");
        }
        return (LokalNachUtc(beginnLokal), endeLokal is { } x ? LokalNachUtc(x) : null);
    }

    private static void VerlangeErstellerOderFuehrung(Termin termin, ClaimsPrincipal handelnder)
    {
        if (handelnder.IstFuehrung())
        {
            return;
        }
        var meId = handelnder.GetAgentId();
        if (!string.IsNullOrEmpty(meId) && termin.ErstelltVonId == meId)
        {
            return;
        }
        throw new UnauthorizedAccessException("Diesen Termin darf nur sein Ersteller oder die Führung bearbeiten.");
    }
}
