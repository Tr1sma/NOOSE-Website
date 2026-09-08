using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Cases;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services.Public;

/// <summary>Turns a citizen concern into an investigation record and links the two.</summary>
public interface ITicketConversionService
{
    /// <summary>Creates a case from the ticket's citizen thread and links it back; returns the new case.</summary>
    Task<Case> ToCaseAsync(string ticketId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="ITicketConversionService" />
/// <remarks>
/// Its own service rather than a method on the ticket service: the ticket side would otherwise have to depend on
/// the case side, and the two record stocks are deliberately separate.
/// <para>
/// The link is written from the CASE to the ticket. A ticket stays a link target only — <c>RecordsReference</c>
/// carries it classified and without an href, and the case page is where the connection belongs. The new case
/// therefore carries the case number of the ticket, never its subject in a place the ticket does not control.
/// </para>
/// </remarks>
public sealed class TicketConversionService(
    IDbContextFactory<AppDbContext> dbFactory,
    ICaseService cases,
    ILinkService links) : ITicketConversionService
{
    private const int MaxLines = 30;

    private const int MaxCharsPerLine = 2_000;

    public async Task<Case> ToCaseAsync(string ticketId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        // the desk decides what becomes a case; the write guard inside runs before the rank check
        Permission.RequireTicketHandling(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ticket = await db.Tickets.AsNoTracking()
            .Where(t => t.Id == ticketId)
            .Select(t => new { t.Id, t.CaseNumber, t.Subject, t.Kind, t.Category })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Ticket nicht gefunden.");
        if (!await TicketVisibility.MayReadAsync(db, ticketId, actor, cancellationToken))
        {
            throw new UnauthorizedAccessException("Du bist an diesem Ticket nicht beteiligt.");
        }

        var audience = ticket.Kind == TicketArt.Intern
            ? TicketMessageAudience.Intern
            : TicketMessageAudience.Buerger;
        // the newest lines, not the oldest: a long-running ticket cut at the front would hand the case the
        // opening small talk and drop the part that actually led to filing it
        var lines = await db.TicketNachrichten.AsNoTracking()
            .Where(m => m.TicketId == ticketId && m.Audience == audience)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Take(MaxLines)
            .Select(m => new { m.Text, m.AuthorIsCitizen, m.CreatedAt })
            .ToListAsync(cancellationToken);
        lines.Reverse();

        var body = new StringBuilder();
        body.AppendLine($"Übernommen aus Ticket {ticket.CaseNumber} ({TicketKategorieDisplay.Name(ticket.Category)}).");
        body.AppendLine();
        foreach (var line in lines)
        {
            // the role, never a name: an agency line carries no agent, and the citizen is named by the ticket alone
            body.AppendLine($"{(line.AuthorIsCitizen ? "Bürger" : "Behörde")}, "
                + $"{line.CreatedAt.ToLocalTime():dd.MM.yyyy HH:mm}:");
            body.AppendLine(Clip(line.Text));
            body.AppendLine();
        }

        var created = await cases.CreateAsync(
            new CaseInput
            {
                Title = ticket.Subject,
                Type = TicketKategorieDisplay.Name(ticket.Category),
                Status = CaseStatus.Open,
                Description = body.ToString().TrimEnd(),
            },
            actor,
            cancellationToken);

        // from the case to the ticket: the connection is shown on the case page, and /tickets/{id} keeps no link panel
        await links.CreateAsync(nameof(Case), created.Id, nameof(Ticket), ticket.Id,
            $"Aus Ticket {ticket.CaseNumber}", actor, cancellationToken: cancellationToken);
        return created;
    }

    private static string Clip(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        return trimmed.Length > MaxCharsPerLine ? trimmed[..MaxCharsPerLine] + " […]" : trimmed;
    }
}
