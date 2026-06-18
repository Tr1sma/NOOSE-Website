using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Models.People;

namespace NOOSE_Website.Services;

/// <summary>Shared helpers for faction and person-group member maintenance.</summary>
internal static class MemberHelper
{
    /// <summary>Returns the existing person id (checked) or creates a fresh person record from a new name.</summary>
    public static async Task<string> PersonIdDetermineAsync(AppDbContext db, IPersonService personService,
        string? personId, string? newName, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(personId) && !string.IsNullOrWhiteSpace(newName))
        {
            var person = await personService.CreateAsync(new PersonInput { Name = newName.Trim() }, actor, cancellationToken);
            return person.Id;
        }
        if (string.IsNullOrWhiteSpace(personId) || !await db.People.AnyAsync(p => p.Id == personId, cancellationToken))
        {
            throw new InvalidOperationException("Die gewählte Person wurde nicht gefunden.");
        }
        return personId;
    }
}
