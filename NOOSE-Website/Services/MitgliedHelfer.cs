using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Personen;

namespace NOOSE_Website.Services;

/// <summary>
/// Gemeinsame Helfer für die Mitglieder-Pflege von Fraktionen und Personengruppen (zuvor je Dienst dupliziert).
/// </summary>
internal static class MitgliedHelfer
{
    /// <summary>
    /// Liefert die Personen-Id: entweder die übergebene bestehende (mit Existenzprüfung) oder – wenn nur ein
    /// neuer Name angegeben ist – eine frisch angelegte Personen-Akte (committet, eigenes Aktenzeichen).
    /// </summary>
    public static async Task<string> PersonIdErmittelnAsync(AppDbContext db, IPersonService personService,
        string? personId, string? neuerName, ClaimsPrincipal handelnder, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(personId) && !string.IsNullOrWhiteSpace(neuerName))
        {
            var person = await personService.ErstellenAsync(new PersonEingabe { Name = neuerName.Trim() }, handelnder, cancellationToken);
            return person.Id;
        }
        if (string.IsNullOrWhiteSpace(personId) || !await db.Personen.AnyAsync(p => p.Id == personId, cancellationToken))
        {
            throw new InvalidOperationException("Die gewählte Person wurde nicht gefunden.");
        }
        return personId;
    }
}
