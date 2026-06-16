using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IKommentarService" />
public class CommentService(IDbContextFactory<AppDbContext> dbFactory, INotificationService notifications) : ICommentService
{
    public Task<List<Comment>> GetForRecordAsync(string entityType, string entityId, bool isLeadership, CancellationToken cancellationToken = default)
        => GetForRecordAsync(entityType, entityId, new ViewerScope(isLeadership, isLeadership, null, null), cancellationToken);

    public async Task<List<Comment>> GetForRecordAsync(string entityType, string entityId, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(db, entityType, entityId, scope, cancellationToken))
        {
            return new();
        }

        var comments = await db.Comments
            .Where(k => k.EntityType == entityType && k.EntityId == entityId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);
        if (scope.PartnerAgency is { } agency)
        {
            comments = await PartnerVisibility.FilterChildrenAsync(db, entityType, entityId, nameof(Comment), comments, c => c.Id, agency, scope.MeId, cancellationToken);
        }
        return comments;
    }

    public async Task<Comment> CreateAsync(string entityType, string entityId, string text, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        text = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Der Kommentar darf nicht leer sein.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // visibility check
        if (!await Visibility.IsRecordVisibleAsync(db, entityType, entityId, actor.IsLeadership(), cancellationToken))
        {
            throw new UnauthorizedAccessException("Diese Akte ist für dich nicht zugänglich.");
        }
        var comment = new Comment
        {
            EntityType = entityType,
            EntityId = entityId,
            Text = text,
            AuthorName = actor.GetCodename(),
        };
        db.Comments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);

        // notify mentions
        try
        {
            var who = string.IsNullOrWhiteSpace(actor.GetCodename()) ? "Ein Agent" : actor.GetCodename();
            await notifications.NotifyMentionedAsync(text, $"{who} hat dich in einem Vermerk erwähnt.",
                SearchNavigation.Route(entityType, entityId), entityType, entityId, actor, cancellationToken);
        }
        catch { /* best effort */ }

        return comment;
    }

    public async Task DeleteAsync(string commentId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var comment = await db.Comments.FirstOrDefaultAsync(k => k.Id == commentId, cancellationToken);
        if (comment is null)
        {
            return;
        }
        // author or leadership
        if (!actor.IsLeadership() && comment.CreatedById != actor.GetAgentId())
        {
            throw new UnauthorizedAccessException("Diesen Kommentar darf nur der Verfasser oder die Führung löschen.");
        }
        db.Comments.Remove(comment); // soft delete
        await db.SaveChangesAsync(cancellationToken);
    }
}
