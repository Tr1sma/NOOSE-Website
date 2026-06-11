using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Personal;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IAgentVerwaltungService" />
// Bewusste Ausnahme von der DbContext-Factory: Dieser Dienst arbeitet eng mit dem UserManager zusammen,
// dessen Identity-Store denselben scoped AppDbContext nutzt. Agent-Änderung und der zugehörige AuditLog
// werden in EINEM Kontext gesammelt und über UserManager.UpdateAsync gespeichert – ein eigener
// Factory-Context würde den hier vorgemerkten AuditLog nicht mitspeichern. Die Admin-Seiten lösen keine
// parallelen Kontextzugriffe aus, daher ist der geteilte scoped Context hier unkritisch.
public class AgentVerwaltungService(UserManager<Agent> userManager, AppDbContext db, INotificationService notifications) : IAgentVerwaltungService
{
    // Reine Lese-/Anzeige-Queries laufen bewusst mit AsNoTracking: Dieser Dienst nutzt einen
    // langlebigen, geteilten scoped AppDbContext (Blazor-Circuit). Würden Anzeige-Queries tracken,
    // gäbe EF bei späteren Abfragen über die Identity-Map eine bereits getrackte, VERALTETE Instanz
    // zurück (Beispiel: ein Agent wird hier geladen, als er noch keinen Antrag hat; stellt er später
    // eine Namensänderung, matcht die WHERE-Query zwar gegen die Live-DB, liefert aber die veraltete
    // In-Memory-Instanz). AsNoTracking materialisiert immer den aktuellen DB-Stand.
    public Task<List<Agent>> GetAusstehendeAsync(CancellationToken cancellationToken = default)
        => db.Users.AsNoTracking().Where(a => a.Status == AgentStatus.Ausstehend)
            .OrderBy(a => a.RegistriertAm)
            .ToListAsync(cancellationToken);

    public Task<List<Agent>> GetAlleAsync(CancellationToken cancellationToken = default)
        => db.Users.AsNoTracking().OrderByDescending(a => a.Status == AgentStatus.Ausstehend)
            .ThenBy(a => a.Codename)
            .ToListAsync(cancellationToken);

    public Task<List<Agent>> GetAuswaehlbareAsync(CancellationToken cancellationToken = default)
        => db.Users.AsNoTracking()
            .Where(a => a.Status == AgentStatus.Aktiv && !a.IstTeamLeitung)
            .OrderBy(a => a.Codename)
            .ToListAsync(cancellationToken);

    public Task<Agent?> FindAsync(string agentId, CancellationToken cancellationToken = default)
        => db.Users.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken);

    public async Task FreigebenAsync(string agentId, Dienstgrad dienstgrad, bool istTRU, ClaimsPrincipal handelnder)
    {
        var agent = await GetOrThrow(agentId);
        var altRang = agent.Dienstgrad;
        agent.Status = AgentStatus.Aktiv;
        agent.Dienstgrad = dienstgrad;
        agent.IstTRU = istTRU;
        agent.FreigegebenAm = DateTime.UtcNow;
        agent.FreigegebenVonId = handelnder.GetAgentId();
        agent.GesperrtGrund = null;

        VerlaufEintragHinzufuegen(agent.Id, altRang, dienstgrad, handelnder, "Erstmalige Freigabe");
        Audit(agent, AuditAktion.Geaendert, handelnder,
            $"Freigegeben als {dienstgrad}{(istTRU ? " (TRU)" : "")}");
        await Speichern(agent, neuerStamp: true);

        // Phase 6: den freigegebenen Agenten benachrichtigen (erscheint beim nächsten Login – der neue
        // SecurityStamp beendet die bisherige Sitzung). Best-effort, eigener Context im Dienst.
        try { await notifications.BenachrichtigeAsync(agent.Id, NotificationTyp.Konto, "Dein Account wurde freigegeben.", "/"); }
        catch { /* Benachrichtigung ist nachrangig. */ }
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
        // Direkt gesetzte Stammdaten sind maßgeblich und lösen einen evtl. offenen Selbst-Antrag auf
        // (z. B. Supervisory+-Selbständerung oder Admin-Eingriff), damit kein widersprüchlicher Antrag zurückbleibt.
        PendingNamensaenderungLeeren(agent);

        Audit(agent, AuditAktion.Geaendert, handelnder, $"Stammdaten geändert (Codename: {agent.Codename})");
        // Neuer Stamp: der betroffene Agent erhält beim nächsten Login frische Claims
        // (eigener Codename/Dienstnummer in Navbar & Begrüßung).
        await Speichern(agent, neuerStamp: true);
    }

    public async Task NamensaenderungBeantragenAsync(string agentId, string? klarname, string codename, string? dienstnummer, ClaimsPrincipal handelnder)
    {
        codename = codename?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(codename))
        {
            throw new InvalidOperationException("Der Codename darf nicht leer sein.");
        }

        var agent = await GetOrThrow(agentId);
        // Vollständiger Schnappschuss des gewünschten Zielzustands; null = Feld soll bei Genehmigung geleert werden.
        agent.AusstehenderCodename = codename;
        agent.AusstehenderKlarname = string.IsNullOrWhiteSpace(klarname) ? null : klarname.Trim();
        agent.AusstehendeDienstnummer = string.IsNullOrWhiteSpace(dienstnummer) ? null : dienstnummer.Trim();
        agent.NamensaenderungBeantragtAm = DateTime.UtcNow;

        Audit(agent, AuditAktion.Geaendert, handelnder, $"Namensänderung beantragt (Codename: {codename})");
        // Kein neuer Stamp: die Live-Identität ändert sich noch nicht, der Antragsteller bleibt eingeloggt.
        await Speichern(agent, neuerStamp: false);
    }

    public Task<List<Agent>> GetAusstehendeNamensaenderungenAsync(CancellationToken cancellationToken = default)
        => db.Users.AsNoTracking().Where(a => a.NamensaenderungBeantragtAm != null)
            .OrderBy(a => a.NamensaenderungBeantragtAm)
            .ToListAsync(cancellationToken);

    public async Task NamensaenderungGenehmigenAsync(string agentId, ClaimsPrincipal handelnder)
    {
        var agent = await GetOrThrow(agentId);
        if (agent.NamensaenderungBeantragtAm is null)
        {
            throw new InvalidOperationException("Für diesen Agent liegt kein Namensänderungs-Antrag vor.");
        }

        // Beantragten Schnappschuss übernehmen (inkl. Leeren von Klarname/Dienstnummer).
        agent.Codename = agent.AusstehenderCodename ?? string.Empty;
        agent.Klarname = agent.AusstehenderKlarname;
        agent.Dienstnummer = agent.AusstehendeDienstnummer;
        PendingNamensaenderungLeeren(agent);

        Audit(agent, AuditAktion.Geaendert, handelnder, $"Namensänderung genehmigt (Codename: {agent.Codename})");
        // Neuer Stamp: der betroffene Agent erhält beim nächsten Login frische Claims (neuer Codename in Navbar).
        await Speichern(agent, neuerStamp: true);

        try { await notifications.BenachrichtigeAsync(agent.Id, NotificationTyp.Konto, "Deine Namensänderung wurde genehmigt.", "/profil"); }
        catch { /* Benachrichtigung ist nachrangig. */ }
    }

    public async Task NamensaenderungAblehnenAsync(string agentId, string grund, ClaimsPrincipal handelnder)
    {
        var agent = await GetOrThrow(agentId);
        if (agent.NamensaenderungBeantragtAm is null)
        {
            throw new InvalidOperationException("Für diesen Agent liegt kein Namensänderungs-Antrag vor.");
        }

        var beantragterCodename = agent.AusstehenderCodename;
        PendingNamensaenderungLeeren(agent);

        var hinweis = string.IsNullOrWhiteSpace(grund) ? "ohne Angabe" : grund.Trim();
        Audit(agent, AuditAktion.Geaendert, handelnder,
            $"Namensänderung abgelehnt (beantragter Codename: {beantragterCodename}): {hinweis}");
        // Kein neuer Stamp: die Live-Identität wurde nicht verändert.
        await Speichern(agent, neuerStamp: false);

        try { await notifications.BenachrichtigeAsync(agent.Id, NotificationTyp.Konto, "Deine Namensänderung wurde abgelehnt.", "/profil"); }
        catch { /* Benachrichtigung ist nachrangig. */ }
    }

    public async Task RangAendernAsync(string agentId, Dienstgrad dienstgrad, ClaimsPrincipal handelnder)
    {
        var agent = await GetOrThrow(agentId);
        var alt = agent.Dienstgrad;
        agent.Dienstgrad = dienstgrad;

        if (alt != dienstgrad)
        {
            VerlaufEintragHinzufuegen(agent.Id, alt, dienstgrad, handelnder, "Rangänderung");
        }
        Audit(agent, AuditAktion.Geaendert, handelnder, $"Dienstgrad {alt?.ToString() ?? "—"} → {dienstgrad}");
        await Speichern(agent, neuerStamp: true);
    }

    public async Task BefoerderungEntscheidenAsync(string antragId, bool genehmigt, string? notiz, ClaimsPrincipal handelnder)
    {
        Berechtigung.VerlangeBefoerderungEntscheiden(handelnder);

        var antrag = await db.AgentBefoerderungsantraege.FirstOrDefaultAsync(a => a.Id == antragId)
            ?? throw new InvalidOperationException($"Beförderungsantrag '{antragId}' nicht gefunden.");
        if (antrag.Status != BefoerderungStatus.Beantragt)
        {
            throw new InvalidOperationException("Über diesen Antrag wurde bereits entschieden.");
        }

        antrag.Status = genehmigt ? BefoerderungStatus.Genehmigt : BefoerderungStatus.Abgelehnt;
        antrag.EntscheiderName = handelnder.GetCodename();
        antrag.EntschiedenAm = DateTime.UtcNow;
        antrag.Entscheidungsnotiz = string.IsNullOrWhiteSpace(notiz) ? null : notiz.Trim();

        var agent = await GetOrThrow(antrag.AgentId);
        if (genehmigt)
        {
            var alt = agent.Dienstgrad;
            agent.Dienstgrad = antrag.ZielDienstgrad;
            if (alt != antrag.ZielDienstgrad)
            {
                VerlaufEintragHinzufuegen(agent.Id, alt, antrag.ZielDienstgrad, handelnder, "Beförderung");
            }
            Audit(agent, AuditAktion.Geaendert, handelnder,
                $"Beförderung genehmigt: {alt?.ToString() ?? "—"} → {antrag.ZielDienstgrad}");
            // Speichern persistiert Agent + Antrag + Verlauf + Audit im geteilten Context und erneuert den Stamp.
            await Speichern(agent, neuerStamp: true);
        }
        else
        {
            Audit(agent, AuditAktion.Geaendert, handelnder,
                $"Beförderungsantrag abgelehnt (Ziel: {antrag.ZielDienstgrad})");
            // Kein Agent-Update → nur den Antrag (+ Audit) im geteilten Context speichern.
            await db.SaveChangesAsync();
        }
    }

    public async Task TruSetzenAsync(string agentId, bool istTRU, ClaimsPrincipal handelnder)
    {
        var agent = await GetOrThrow(agentId);
        agent.IstTRU = istTRU;

        Audit(agent, AuditAktion.Geaendert, handelnder, istTRU ? "TRU-Flag gesetzt" : "TRU-Flag entfernt");
        await Speichern(agent, neuerStamp: true);
    }

    public async Task TeamLeitungSetzenAsync(string agentId, bool istTeamLeitung, ClaimsPrincipal handelnder)
    {
        var agent = await GetOrThrow(agentId);
        agent.IstTeamLeitung = istTeamLeitung;

        Audit(agent, AuditAktion.Geaendert, handelnder,
            istTeamLeitung ? "Als TeamLeitung markiert" : "TeamLeitung-Markierung entfernt");
        // Bewusst KEIN neuer SecurityStamp: Der Marker steht in keinem Claim und verändert nichts an der eigenen
        // Sitzung des Betroffenen – er steuert nur, ob ANDERE den Agenten in Auswahl-/Erwähnungslisten sehen.
        await Speichern(agent, neuerStamp: false);
    }

    public async Task AdminSetzenAsync(string agentId, bool istAdmin, ClaimsPrincipal handelnder)
    {
        var agent = await GetOrThrow(agentId);

        // Selbst-Aussperrung und das Entfernen des letzten Admins serverseitig verhindern (nicht nur via UI).
        if (!istAdmin && agent.IstAdmin)
        {
            if (handelnder.GetAgentId() == agentId)
            {
                throw new InvalidOperationException("Du kannst dir nicht selbst die Admin-Rechte entziehen.");
            }
            if (await db.Users.CountAsync(u => u.IstAdmin) <= 1)
            {
                throw new InvalidOperationException("Der letzte verbliebene Admin kann nicht entfernt werden.");
            }
        }

        agent.IstAdmin = istAdmin;

        Audit(agent, AuditAktion.Geaendert, handelnder, istAdmin ? "Admin-Rechte vergeben" : "Admin-Rechte entzogen");
        await Speichern(agent, neuerStamp: true);
    }

    public async Task SperrenAsync(string agentId, string grund, ClaimsPrincipal handelnder)
    {
        // Sich selbst zu sperren würde die eigene Sitzung sofort beenden – das ist fast immer ein Fehlgriff.
        if (handelnder.GetAgentId() == agentId)
        {
            throw new InvalidOperationException("Du kannst dich nicht selbst sperren.");
        }

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

    private static void PendingNamensaenderungLeeren(Agent agent)
    {
        agent.AusstehenderCodename = null;
        agent.AusstehenderKlarname = null;
        agent.AusstehendeDienstnummer = null;
        agent.NamensaenderungBeantragtAm = null;
    }

    // Schreibt einen Dienstgrad-Verlaufseintrag in den geteilten Context (wird mit Speichern/SaveChanges persistiert).
    private void VerlaufEintragHinzufuegen(string agentId, Dienstgrad? alt, Dienstgrad neu, ClaimsPrincipal handelnder, string grund)
        => db.AgentDienstgradVerlaeufe.Add(new AgentDienstgradVerlauf
        {
            AgentId = agentId,
            Alt = alt,
            Neu = neu,
            Zeitpunkt = DateTime.UtcNow,
            AkteurName = handelnder.GetCodename(),
            Grund = grund,
        });

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
        // IdentityResult auswerten – sonst meldet die UI „gespeichert", obwohl das Update fehlschlug
        // (besonders kritisch beim Kill-Switch). Bei Fehlern als Exception eskalieren.
        var result = await userManager.UpdateAsync(agent);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Speichern fehlgeschlagen: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }
        if (neuerStamp)
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
