using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Models.People;

namespace NOOSE_Website.Services;

/// <summary>
/// Gemeinsame Helfer für die Mitglieder-Pflege von Fraktionen und Personengruppen (zuvor je Dienst dupliziert).
/// </summary>
internal static class MemberHelper
{
    /// <summary>
    /// Liefert die Personen-Id: entweder die übergebene bestehende (mit Existenzprüfung) oder – wenn nur ein
    /// neuer Name angegeben ist – eine frisch angelegte Personen-Akte (committet, eigenes Aktenzeichen).
    /// </summary>
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
