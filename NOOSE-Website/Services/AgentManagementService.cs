using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Personnel;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IAgentVerwaltungService" />
// SCHREIBEN: Bewusste Ausnahme von der DbContext-Factory – dieser Dienst arbeitet eng mit dem UserManager
// zusammen, dessen Identity-Store denselben scoped AppDbContext nutzt. Agent-Änderung und der zugehörige
// AuditLog werden in EINEM Kontext gesammelt und über UserManager.UpdateAsync gespeichert – ein eigener
// Factory-Context würde den hier vorgemerkten AuditLog nicht mitspeichern.
// LESEN: läuft dagegen über per-Operation-Factory-Kontexte. Grund: Die NavMenu (Layout, Freigaben-Badge)
// und die gerouteten Seiten (PersonalListe/Freigaben/Agenten) initialisieren PARALLEL und riefen zuvor
// Lese-Methoden auf demselben scoped Context auf → race-abhängig „A second operation was started on this
// context instance" → während des Prerenders unbehandelt → generische Fehlerseite beim Neuladen.
public class AgentManagementService(
    UserManager<Agent> userManager,
    AppDbContext db,
    IDbContextFactory<AppDbContext> dbFactory,
    INotificationService notifications) : IAgentManagementService
{
    // Lese-/Anzeige-Queries: eigener kurzlebiger Factory-Context je Aufruf (parallel-sicher, s. o.).
    // AsNoTracking bleibt: reine Anzeige-Daten brauchen kein Change-Tracking.
    public async Task<List<Agent>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        await using var readDb = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await readDb.Users.AsNoTracking().Where(a => a.Status == AgentStatus.Pending)
            .OrderBy(a => a.RegisteredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Agent>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var readDb = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await readDb.Users.AsNoTracking().OrderByDescending(a => a.Status == AgentStatus.Pending)
            .ThenBy(a => a.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Agent>> GetSelectableAsync(CancellationToken cancellationToken = default)
    {
        await using var readDb = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await readDb.Users.AsNoTracking()
            .Where(a => a.Status == AgentStatus.Active && !a.IsTeamLead)
            .OrderBy(a => a.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task<Agent?> FindAsync(string agentId, CancellationToken cancellationToken = default)
    {
        await using var readDb = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await readDb.Users.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken);
    }

    public async Task ReleaseAsync(string agentId, Rank rank, bool isTRU, bool isHRB, ClaimsPrincipal actor)
    {
        var agent = await GetOrThrow(agentId);
        var altRank = agent.Rank;
        agent.Status = AgentStatus.Active;
        agent.Rank = rank;
        agent.IsTRU = isTRU;
        agent.IsHRB = isHRB;
        agent.ReleasedAt = DateTime.UtcNow;
        agent.ReleasedById = actor.GetAgentId();
        agent.BlockedReason = null;

        HistoryEntryAdd(agent.Id, altRank, rank, actor, "Erstmalige Freigabe");
        Audit(agent, AuditAction.Modified, actor,
            $"Freigegeben als {rank}{(isTRU ? " (TRU)" : "")}{(isHRB ? " (HRB)" : "")}");
        await Save(agent, newStamp: true);

        // Phase 6: den freigegebenen Agenten benachrichtigen (erscheint beim nächsten Login – der neue
        // SecurityStamp beendet die bisherige Sitzung). Best-effort, eigener Context im Dienst.
        try { await notifications.NotifyAsync(agent.Id, NotificationType.Account, "Dein Account wurde freigegeben.", "/"); }
        catch { /* Benachrichtigung ist nachrangig. */ }
    }

    public async Task RejectAsync(string agentId, string reason, ClaimsPrincipal actor)
    {
        var agent = await GetOrThrow(agentId);
        agent.Status = AgentStatus.Blocked;
        agent.BlockedReason = string.IsNullOrWhiteSpace(reason) ? "Registrierung abgelehnt" : reason;

        Audit(agent, AuditAction.Modified, actor, $"Registrierung abgelehnt: {agent.BlockedReason}");
        await Save(agent, newStamp: true);
    }

    public async Task MasterDataChangeAsync(string agentId, string? realName, string codename, string? badgeNumber, ClaimsPrincipal actor)
    {
        codename = codename?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(codename))
        {
            throw new InvalidOperationException("Der Codename darf nicht leer sein.");
        }

        var agent = await GetOrThrow(agentId);
        agent.RealName = string.IsNullOrWhiteSpace(realName) ? null : realName.Trim();
        agent.Codename = codename;
        agent.BadgeNumber = string.IsNullOrWhiteSpace(badgeNumber) ? null : badgeNumber.Trim();
        // Direkt gesetzte Stammdaten sind maßgeblich und lösen einen evtl. offenen Selbst-Antrag auf
        // (z. B. Supervisory+-Selbständerung oder Admin-Eingriff), damit kein widersprüchlicher Antrag zurückbleibt.
        PendingNameChangeEmpty(agent);

        Audit(agent, AuditAction.Modified, actor, $"Stammdaten geändert (Codename: {agent.Codename})");
        // Neuer Stamp: der betroffene Agent erhält beim nächsten Login frische Claims
        // (eigener Codename/Dienstnummer in Navbar & Begrüßung).
        await Save(agent, newStamp: true);
    }

    public async Task NameChangeRequestAsync(string agentId, string? realName, string codename, string? badgeNumber, ClaimsPrincipal actor)
    {
        codename = codename?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(codename))
        {
            throw new InvalidOperationException("Der Codename darf nicht leer sein.");
        }

        var agent = await GetOrThrow(agentId);
        // Vollständiger Schnappschuss des gewünschten Zielzustands; null = Feld soll bei Genehmigung geleert werden.
        agent.PendingCodename = codename;
        agent.PendingRealName = string.IsNullOrWhiteSpace(realName) ? null : realName.Trim();
        agent.PendingBadgeNumber = string.IsNullOrWhiteSpace(badgeNumber) ? null : badgeNumber.Trim();
        agent.NameChangeRequestedAt = DateTime.UtcNow;

        Audit(agent, AuditAction.Modified, actor, $"Namensänderung beantragt (Codename: {codename})");
        // Kein neuer Stamp: die Live-Identität ändert sich noch nicht, der Antragsteller bleibt eingeloggt.
        await Save(agent, newStamp: false);
    }

    public async Task<List<Agent>> GetPendingNameChangesAsync(CancellationToken cancellationToken = default)
    {
        await using var readDb = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await readDb.Users.AsNoTracking().Where(a => a.NameChangeRequestedAt != null)
            .OrderBy(a => a.NameChangeRequestedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task NameChangeApproveAsync(string agentId, ClaimsPrincipal actor)
    {
        var agent = await GetOrThrow(agentId);
        if (agent.NameChangeRequestedAt is null)
        {
            throw new InvalidOperationException("Für diesen Agent liegt kein Namensänderungs-Antrag vor.");
        }

        // Beantragten Schnappschuss übernehmen (inkl. Leeren von Klarname/Dienstnummer).
        agent.Codename = agent.PendingCodename ?? string.Empty;
        agent.RealName = agent.PendingRealName;
        agent.BadgeNumber = agent.PendingBadgeNumber;
        PendingNameChangeEmpty(agent);

        Audit(agent, AuditAction.Modified, actor, $"Namensänderung genehmigt (Codename: {agent.Codename})");
        // Neuer Stamp: der betroffene Agent erhält beim nächsten Login frische Claims (neuer Codename in Navbar).
        await Save(agent, newStamp: true);

        try { await notifications.NotifyAsync(agent.Id, NotificationType.Account, "Deine Namensänderung wurde genehmigt.", "/profil"); }
        catch { /* Benachrichtigung ist nachrangig. */ }
    }

    public async Task NameChangeRejectAsync(string agentId, string reason, ClaimsPrincipal actor)
    {
        var agent = await GetOrThrow(agentId);
        if (agent.NameChangeRequestedAt is null)
        {
            throw new InvalidOperationException("Für diesen Agent liegt kein Namensänderungs-Antrag vor.");
        }

        var requestedCodename = agent.PendingCodename;
        PendingNameChangeEmpty(agent);

        var hint = string.IsNullOrWhiteSpace(reason) ? "ohne Angabe" : reason.Trim();
        Audit(agent, AuditAction.Modified, actor,
            $"Namensänderung abgelehnt (beantragter Codename: {requestedCodename}): {hint}");
        // Kein neuer Stamp: die Live-Identität wurde nicht verändert.
        await Save(agent, newStamp: false);

        try { await notifications.NotifyAsync(agent.Id, NotificationType.Account, "Deine Namensänderung wurde abgelehnt.", "/profil"); }
        catch { /* Benachrichtigung ist nachrangig. */ }
    }

    public async Task RankChangeAsync(string agentId, Rank rank, ClaimsPrincipal actor)
    {
        Permission.RequireLeadership(actor);

        var agent = await GetOrThrow(agentId);
        var alt = agent.Rank;
        agent.Rank = rank;

        if (alt != rank)
        {
            HistoryEntryAdd(agent.Id, alt, rank, actor, "Rangänderung");
        }
        Audit(agent, AuditAction.Modified, actor, $"Dienstgrad {alt?.ToString() ?? "—"} → {rank}");
        await Save(agent, newStamp: true);
    }

    public async Task PromotionDecideAsync(string requestId, bool approved, string? note, ClaimsPrincipal actor)
    {
        Permission.RequirePromotionDecide(actor);

        var request = await db.AgentPromotionRequests.FirstOrDefaultAsync(a => a.Id == requestId)
            ?? throw new InvalidOperationException($"Beförderungsantrag '{requestId}' nicht gefunden.");
        if (request.Status != PromotionStatus.Requested)
        {
            throw new InvalidOperationException("Über diesen Antrag wurde bereits entschieden.");
        }

        request.Status = approved ? PromotionStatus.Approved : PromotionStatus.Rejected;
        request.DeciderName = actor.GetCodename();
        request.DecidedAt = DateTime.UtcNow;
        request.DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        var agent = await GetOrThrow(request.AgentId);
        if (approved)
        {
            var alt = agent.Rank;
            agent.Rank = request.TargetRank;
            if (alt != request.TargetRank)
            {
                HistoryEntryAdd(agent.Id, alt, request.TargetRank, actor, "Beförderung");
            }
            Audit(agent, AuditAction.Modified, actor,
                $"Beförderung genehmigt: {alt?.ToString() ?? "—"} → {request.TargetRank}");
            // Speichern persistiert Agent + Antrag + Verlauf + Audit im geteilten Context und erneuert den Stamp.
            await Save(agent, newStamp: true);
        }
        else
        {
            Audit(agent, AuditAction.Modified, actor,
                $"Beförderungsantrag abgelehnt (Ziel: {request.TargetRank})");
            // Kein Agent-Update → nur den Antrag (+ Audit) im geteilten Context speichern.
            await db.SaveChangesAsync();
        }
    }

    public async Task TruSetAsync(string agentId, bool isTRU, ClaimsPrincipal actor)
    {
        Permission.RequireLeadership(actor);

        var agent = await GetOrThrow(agentId);
        agent.IsTRU = isTRU;

        Audit(agent, AuditAction.Modified, actor, isTRU ? "TRU-Flag gesetzt" : "TRU-Flag entfernt");
        await Save(agent, newStamp: true);
    }

    public async Task HrbSetAsync(string agentId, bool isHRB, ClaimsPrincipal actor)
    {
        Permission.RequireLeadership(actor);

        var agent = await GetOrThrow(agentId);
        agent.IsHRB = isHRB;

        Audit(agent, AuditAction.Modified, actor, isHRB ? "HRB-Flag gesetzt" : "HRB-Flag entfernt");
        await Save(agent, newStamp: true);
    }

    public async Task TeamLeadSetAsync(string agentId, bool isTeamLead, ClaimsPrincipal actor)
    {
        Permission.RequireLeadership(actor);
        Permission.RequireWriteAccess(actor);

        var agent = await GetOrThrow(agentId);
        agent.IsTeamLead = isTeamLead;

        Audit(agent, AuditAction.Modified, actor,
            isTeamLead ? "Als Teamleitung markiert" : "Teamleitung-Markierung entfernt");
        // Neuer SecurityStamp: TeamLeitung ist jetzt ein Claim (steuert die Nur-Lese-Aufsichtsrolle). Die
        // Stamp-Rotation beendet die Sitzungen des Betroffenen, damit der geänderte Claim – und damit der
        // Lese-/Schreibumfang – beim nächsten Login wirksam wird.
        await Save(agent, newStamp: true);
    }

    public async Task AdminSetAsync(string agentId, bool isAdmin, ClaimsPrincipal actor)
    {
        // Admin-Rechte vergeben/entziehen ist ausschließlich Admins vorbehalten – die Führung erreicht diese
        // Seite zwar, darf aber niemanden zum Admin machen. Harte serverseitige Garantie (nicht nur via UI).
        Permission.RequireAdmin(actor);

        var agent = await GetOrThrow(agentId);

        // Selbst-Aussperrung und das Entfernen des letzten Admins serverseitig verhindern (nicht nur via UI).
        if (!isAdmin && agent.IsAdmin)
        {
            if (actor.GetAgentId() == agentId)
            {
                throw new InvalidOperationException("Du kannst dir nicht selbst die Admin-Rechte entziehen.");
            }
            if (await db.Users.CountAsync(u => u.IsAdmin) <= 1)
            {
                throw new InvalidOperationException("Der letzte verbliebene Admin kann nicht entfernt werden.");
            }
        }

        agent.IsAdmin = isAdmin;

        Audit(agent, AuditAction.Modified, actor, isAdmin ? "Admin-Rechte vergeben" : "Admin-Rechte entzogen");
        await Save(agent, newStamp: true);
    }

    public async Task BlockAsync(string agentId, string reason, ClaimsPrincipal actor)
    {
        Permission.RequireLeadership(actor);

        // Sich selbst zu sperren würde die eigene Sitzung sofort beenden – das ist fast immer ein Fehlgriff.
        if (actor.GetAgentId() == agentId)
        {
            throw new InvalidOperationException("Du kannst dich nicht selbst sperren.");
        }

        var agent = await GetOrThrow(agentId);
        agent.Status = AgentStatus.Blocked;
        agent.BlockedReason = string.IsNullOrWhiteSpace(reason) ? "Notfall-Sperre" : reason;

        Audit(agent, AuditAction.Modified, actor, $"Gesperrt (Kill-Switch): {agent.BlockedReason}");
        await Save(agent, newStamp: true);
    }

    public async Task UnblockAsync(string agentId, ClaimsPrincipal actor)
    {
        Permission.RequireLeadership(actor);

        var agent = await GetOrThrow(agentId);
        agent.Status = AgentStatus.Active;
        agent.BlockedReason = null;

        Audit(agent, AuditAction.Modified, actor, "Entsperrt");
        await Save(agent, newStamp: true);
    }

    // Lädt den Agenten für eine Mutation. Da der geteilte Context (Blazor-Circuit) langlebig ist und
    // evtl. noch eine getrackte Instanz aus einer früheren Operation hält, wird der Datensatz frisch
    // aus der DB nachgeladen (ReloadAsync) – sonst entschiede eine Mutation evtl. auf veraltetem Stand
    // (z. B. „kein Namensänderungs-Antrag", obwohl in der DB längst einer vorliegt).
    private async Task<Agent> GetOrThrow(string agentId)
    {
        var agent = await db.Users.FirstOrDefaultAsync(a => a.Id == agentId)
            ?? throw new InvalidOperationException($"Agent '{agentId}' nicht gefunden.");
        await db.Entry(agent).ReloadAsync();
        return agent;
    }

    private static void PendingNameChangeEmpty(Agent agent)
    {
        agent.PendingCodename = null;
        agent.PendingRealName = null;
        agent.PendingBadgeNumber = null;
        agent.NameChangeRequestedAt = null;
    }

    // Schreibt einen Dienstgrad-Verlaufseintrag in den geteilten Context (wird mit Speichern/SaveChanges persistiert).
    private void HistoryEntryAdd(string agentId, Rank? alt, Rank @new, ClaimsPrincipal actor, string reason)
        => db.AgentRankHistories.Add(new AgentRankHistory
        {
            AgentId = agentId,
            Alt = alt,
            New = @new,
            Timestamp = DateTime.UtcNow,
            ActorName = actor.GetCodename(),
            Reason = reason,
        });

    private void Audit(Agent target, AuditAction action, ClaimsPrincipal actor, string hint)
        => db.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            AgentId = actor.GetAgentId(),
            AgentName = actor.GetCodename(),
            EntityType = nameof(Agent),
            EntityId = target.Id,
            Action = action,
            ChangesJson = JsonSerializer.Serialize(new { target = target.Codename, hint }),
        });

    /// <summary>
    /// Persistiert den Agent (samt offenem AuditLog im selben Kontext). Bei <paramref name="neuerStamp"/>
    /// wird zusätzlich der SecurityStamp erneuert – das invalidiert alle bestehenden Cookies des
    /// Agents (Sitzungen enden) und erzwingt beim nächsten Login frische Claims/Rechte.
    /// </summary>
    private async Task Save(Agent agent, bool newStamp)
    {
        // IdentityResult auswerten – sonst meldet die UI „gespeichert", obwohl das Update fehlschlug
        // (besonders kritisch beim Kill-Switch). Bei Fehlern als Exception eskalieren.
        var result = await userManager.UpdateAsync(agent);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Speichern fehlgeschlagen: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }
        if (newStamp)
        {
            var stamp = await userManager.UpdateSecurityStampAsync(agent);
            if (!stamp.Succeeded)
            {
                throw new InvalidOperationException(
                    "Sicherheits-Stamp konnte nicht erneuert werden: " + string.Join("; ", stamp.Errors.Select(e => e.Description)));
            }
        }
    }
}
