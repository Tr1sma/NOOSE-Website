using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ITextSnippetService" />
public class TextSnippetService(IDbContextFactory<AppDbContext> dbFactory) : ITextSnippetService
{
    /// <summary>Most a single agent may keep.</summary>
    /// <remarks>
    /// A list nobody can survey any more stops being used, and the picker shows it in one go. The cap sits here
    /// rather than in the panel: the panel is not the only way in once a second surface exists.
    /// </remarks>
    public const int MaxPerAgent = 50;

    public async Task<List<TextSnippet>> GetMineAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var meId = actor.GetAgentId();
        if (string.IsNullOrWhiteSpace(meId))
        {
            return [];
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Textbausteine
            .Where(s => s.AgentId == meId)
            .OrderBy(s => s.Sorting).ThenBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<TextSnippet> CreateAsync(TextSnippetInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var meId = RequireMine(actor);
        var name = Name(input);
        var text = Text(input);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Textbausteine.CountAsync(s => s.AgentId == meId, cancellationToken) >= MaxPerAgent)
        {
            throw new InvalidOperationException($"Mehr als {MaxPerAgent} Textbausteine sind nicht vorgesehen.");
        }
        if (await db.Textbausteine.AnyAsync(s => s.AgentId == meId && s.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Ein Textbaustein „{name}“ existiert bereits.");
        }

        var snippet = new TextSnippet
        {
            AgentId = meId,
            Name = name,
            Text = text,
            Sorting = input.Sorting,
        };
        db.Textbausteine.Add(snippet);
        await db.SaveChangesAsync(cancellationToken);
        return snippet;
    }

    public async Task RefreshAsync(string id, TextSnippetInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var meId = RequireMine(actor);
        var name = Name(input);
        var text = Text(input);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // the owner is part of the lookup, not a check afterwards: a foreign id simply finds nothing
        var snippet = await db.Textbausteine.FirstOrDefaultAsync(s => s.Id == id && s.AgentId == meId, cancellationToken)
            ?? throw new InvalidOperationException("Dieser Textbaustein gehört dir nicht.");
        if (await db.Textbausteine.AnyAsync(s => s.AgentId == meId && s.Id != id && s.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Ein Textbaustein „{name}“ existiert bereits.");
        }

        snippet.Name = name;
        snippet.Text = text;
        snippet.Sorting = input.Sorting;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var meId = RequireMine(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var snippet = await db.Textbausteine.FirstOrDefaultAsync(s => s.Id == id && s.AgentId == meId, cancellationToken)
            ?? throw new InvalidOperationException("Dieser Textbaustein gehört dir nicht.");
        // no soft delete: a private convenience has no business in the leadership-readable bin
        db.Textbausteine.Remove(snippet);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The caller's own agent id, or a refusal. Nobody ever names the owner from outside.</summary>
    private static string RequireMine(ClaimsPrincipal actor)
    {
        Permission.RequireWriteAccess(actor);
        var meId = actor.GetAgentId();
        return string.IsNullOrWhiteSpace(meId)
            ? throw new UnauthorizedAccessException("Kein angemeldeter Agent.")
            : meId;
    }

    private static string Name(TextSnippetInput input)
    {
        var name = (input.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Textbaustein braucht einen Namen.");
        }
        return name.Length > 80 ? name[..80] : name;
    }

    private static string Text(TextSnippetInput input)
    {
        var text = (input.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Der Textbaustein darf nicht leer sein.");
        }
        return text.Length > 4000 ? text[..4000] : text;
    }
}
