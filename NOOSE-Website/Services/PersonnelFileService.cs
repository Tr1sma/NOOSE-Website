using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Personnel;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IPersonalakteService" />
public class PersonnelFileService(IDbContextFactory<AppDbContext> dbFactory) : IPersonnelFileService
{
    public async Task<List<AgentRankHistory>> GetRankHistoryAsync(string agentId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AgentRankHistories
            .Where(v => v.AgentId == agentId)
            .OrderByDescending(v => v.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AgentNote>> GetNotesAsync(string agentId, AgentNoteKind kind, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AgentNotes
            .Where(v => v.AgentId == agentId && v.Kind == kind)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<AgentNote> NoteCreateAsync(string agentId, AgentNoteKind kind, string text, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        var content = text?.Trim();
        if (string.IsNullOrEmpty(content))
        {
            throw new InvalidOperationException("Der Vermerk darf nicht leer sein.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Users.AnyAsync(u => u.Id == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Der gewählte Agent wurde nicht gefunden.");
        }
        var note = new AgentNote
        {
            AgentId = agentId,
            Kind = kind,
            Text = content,
            AuthorName = actor.GetCodename(),
        };
        db.AgentNotes.Add(note);
        await db.SaveChangesAsync(cancellationToken);
        return note;
    }

    public async Task NoteDeleteAsync(string noteId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var note = await db.AgentNotes.FirstOrDefaultAsync(v => v.Id == noteId, cancellationToken);
        if (note is null)
        {
            return;
        }
        // Löschen darf der Verfasser selbst oder die Führung – serverseitig erzwingen.
        if (!actor.IsLeadership() && note.CreatedById != actor.GetAgentId())
        {
            throw new UnauthorizedAccessException("Diesen Vermerk darf nur der Verfasser oder die Führung löschen.");
        }
        db.AgentNotes.Remove(note); // Soft-Delete via Interceptor
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<AgentPromotionRequest>> GetPromotionRequestsAsync(string agentId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AgentPromotionRequests
            .Where(a => a.AgentId == agentId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AgentPromotionRequest>> GetOpenPromotionRequestsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AgentPromotionRequests
            .Where(a => a.Status == PromotionStatus.Requested)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<AgentPromotionRequest> PromotionRequestAsync(string agentId, Rank targetRank, string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Users.AnyAsync(u => u.Id == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Der gewählte Agent wurde nicht gefunden.");
        }
        if (await db.AgentPromotionRequests.AnyAsync(a => a.AgentId == agentId && a.Status == PromotionStatus.Requested, cancellationToken))
        {
            throw new InvalidOperationException("Für diesen Agenten ist bereits ein Beförderungsantrag offen.");
        }
        var request = new AgentPromotionRequest
        {
            AgentId = agentId,
            TargetRank = targetRank,
            Justification = string.IsNullOrWhiteSpace(justification) ? null : justification.Trim(),
            Status = PromotionStatus.Requested,
            RequesterName = actor.GetCodename(),
        };
        db.AgentPromotionRequests.Add(request);
        await db.SaveChangesAsync(cancellationToken);
        return request;
    }
}
