using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IAgentVerwaltungService" />
public class AgentVerwaltungService(UserManager<Agent> userManager, AppDbContext db) : IAgentVerwaltungService
{
    public Task<List<Agent>> GetAusstehendeAsync(CancellationToken cancellationToken = default)
        => db.Users.Where(a => a.Status == AgentStatus.Ausstehend)
            .OrderBy(a => a.RegistriertAm)
            .ToListAsync(cancellationToken);

    public Task<List<Agent>> GetAlleAsync(CancellationToken cancellationToken = default)
        => db.Users.OrderByDescending(a => a.Status == AgentStatus.Ausstehend)
            .ThenBy(a => a.Codename)
            .ToListAsync(cancellationToken);

    public Task<Agent?> FindAsync(string agentId, CancellationToken cancellationToken = default)
        => db.Users.FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken);

    public async Task FreigebenAsync(string agentId, Dienstgrad dienstgrad, bool istTRU, ClaimsPrincipal handelnder)
    {
        var agent = await GetOrThrow(agentId);
        agent.Status = AgentStatus.Aktiv;
        agent.Dienstgrad = dienstgrad;
        agent.IstTRU = istTRU;
        agent.FreigegebenAm = DateTime.UtcNow;
        agent.FreigegebenVonId = handelnder.GetAgentId();
        agent.GesperrtGrund = null;

        Audit(agent, AuditAktion.Geaendert, handelnder,
            $"Freigegeben als {dienstgrad}{(istTRU ? " (TRU)" : "")}");
        await Speichern(agent, neuerStamp: true);
    }

    public async Task AblehnenAsync(string agentId, string grund, ClaimsPrincipal handelnder)
    {
        var agent = await GetOrThrow(agentId);
        agent.Status = AgentStatus.Gesperrt;
        agent.GesperrtGrund = string.IsNullOrWhiteSpace(grund) ? "Registrierung abgelehnt" : grund;

        Audit(agent, AuditAktion.Geaendert, handelnder, $"Registrierung abgelehnt: {agent.GesperrtGrund}");
        await Speichern(agent, neuerStamp: true);
    }

    public async Task StammdatenAendernAsync(string agentId, string? klarname, string codename, string? dienstnummer, ClaimsPrincipal handelnder)
    {
        codename = codename?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(codename))
        {
            throw new InvalidOperationException("Der Codename darf nicht leer sein.");
        }

        var agent = await GetOrThrow(agentId);
        agent.Klarname = string.IsNullOrWhiteSpace(klarname) ? null : klarname.Trim();
        agent.Codename = codename;
        agent.Dienstnummer = string.IsNullOrWhiteSpace(dienstnummer) ? null : dienstnummer.Trim();

        Audit(agent, AuditAktion.Geaendert, handelnder, $"Stammdaten geändert (Codename: {agent.Codename})");
        // Neuer Stamp: der betroffene Agent erhält beim nächsten Login frische Claims
        // (eigener Codename/Dienstnummer in Navbar & Begrüßung).
        await Speichern(agent, neuerStamp: true);
    }

    public async Task RangAendernAsync(string agentId, Dienstgrad dienstgrad, ClaimsPrincipal handelnder)
    {
        var agent = await GetOrThrow(agentId);
        var alt = agent.Dienstgrad;
        agent.Dienstgrad = dienstgrad;

        Audit(agent, AuditAktion.Geaendert, handelnder, $"Dienstgrad {alt?.ToString() ?? "—"} → {dienstgrad}");
        await Speichern(agent, neuerStamp: true);
    }

    public async Task TruSetzenAsync(string agentId, bool istTRU, ClaimsPrincipal handelnder)
    {
        var agent = await GetOrThrow(agentId);
        agent.IstTRU = istTRU;

        Audit(agent, AuditAktion.Geaendert, handelnder, istTRU ? "TRU-Flag gesetzt" : "TRU-Flag entfernt");
        await Speichern(agent, neuerStamp: true);
    }

    public async Task AdminSetzenAsync(string agentId, bool istAdmin, ClaimsPrincipal handelnder)
    {
        var agent = await GetOrThrow(agentId);
        agent.IstAdmin = istAdmin;

        Audit(agent, AuditAktion.Geaendert, handelnder, istAdmin ? "Admin-Rechte vergeben" : "Admin-Rechte entzogen");
        await Speichern(agent, neuerStamp: true);
    }

    public async Task SperrenAsync(string agentId, string grund, ClaimsPrincipal handelnder)
    {
        var agent = await GetOrThrow(agentId);
        agent.Status = AgentStatus.Gesperrt;
        agent.GesperrtGrund = string.IsNullOrWhiteSpace(grund) ? "Notfall-Sperre" : grund;

        Audit(agent, AuditAktion.Geaendert, handelnder, $"Gesperrt (Kill-Switch): {agent.GesperrtGrund}");
        await Speichern(agent, neuerStamp: true);
    }

    public async Task EntsperrenAsync(string agentId, ClaimsPrincipal handelnder)
    {
        var agent = await GetOrThrow(agentId);
        agent.Status = AgentStatus.Aktiv;
        agent.GesperrtGrund = null;

        Audit(agent, AuditAktion.Geaendert, handelnder, "Entsperrt");
        await Speichern(agent, neuerStamp: true);
    }

    private async Task<Agent> GetOrThrow(string agentId)
        => await db.Users.FirstOrDefaultAsync(a => a.Id == agentId)
           ?? throw new InvalidOperationException($"Agent '{agentId}' nicht gefunden.");

    private void Audit(Agent ziel, AuditAktion aktion, ClaimsPrincipal handelnder, string hinweis)
        => db.AuditLogs.Add(new AuditLog
        {
            Zeitpunkt = DateTime.UtcNow,
            AgentId = handelnder.GetAgentId(),
            AgentName = handelnder.GetCodename(),
            EntitaetTyp = nameof(Agent),
            EntitaetId = ziel.Id,
            Aktion = aktion,
            AenderungenJson = JsonSerializer.Serialize(new { ziel = ziel.Codename, hinweis }),
        });

    /// <summary>
    /// Persistiert den Agent (samt offenem AuditLog im selben Kontext). Bei <paramref name="neuerStamp"/>
    /// wird zusätzlich der SecurityStamp erneuert – das invalidiert alle bestehenden Cookies des
    /// Agents (Sitzungen enden) und erzwingt beim nächsten Login frische Claims/Rechte.
    /// </summary>
    private async Task Speichern(Agent agent, bool neuerStamp)
    {
        await userManager.UpdateAsync(agent);
        if (neuerStamp)
        {
            await userManager.UpdateSecurityStampAsync(agent);
        }
    }
}
