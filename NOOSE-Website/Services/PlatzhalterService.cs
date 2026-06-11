using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Aufgaben;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Operationen;
using NOOSE_Website.Data.Entities.Parteien;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Vorgaenge;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IPlatzhalterService" />
public partial class PlatzhalterService(IDbContextFactory<AppDbContext> dbFactory) : IPlatzhalterService
{
    public IReadOnlyList<(string Token, string Beschreibung)> VerfuegbarePlatzhalter { get; } = new[]
    {
        ("{{Name}}", "Name der Akte, an die das Dokument gehängt wird"),
        ("{{Aktenzeichen}}", "Aktenzeichen dieser Akte"),
        ("{{Datum}}", "Aktuelles Datum (TT.MM.JJJJ)"),
        ("{{Uhrzeit}}", "Aktuelle Uhrzeit (HH:MM)"),
        ("{{Agent}}", "Dein Codename"),
        ("{{Dienstgrad}}", "Dein Dienstgrad"),
    };

    public async Task<string> AnwendenAsync(string html, string? entitaetTyp, string? entitaetId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(html))
        {
            return html ?? string.Empty;
        }

        var name = string.Empty;
        var aktenzeichen = string.Empty;

        if (!string.IsNullOrWhiteSpace(entitaetTyp) && !string.IsNullOrWhiteSpace(entitaetId))
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            // Nur sichtbare Akten auflösen (keine Verschlusssachen-Namen für Nicht-Führung).
            if (await Sichtbarkeit.IstAkteSichtbarAsync(db, entitaetTyp!, entitaetId!, handelnder.IstFuehrung(), cancellationToken))
            {
                var akte = await AkteNameAsync(db, entitaetTyp!, entitaetId!, cancellationToken);
                name = akte.Name;
                aktenzeichen = akte.Aktenzeichen;
            }
        }

        var jetzt = DateTime.Now;
        var ersetzungen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = name,
            ["Aktenzeichen"] = aktenzeichen,
            ["Datum"] = jetzt.ToString("dd.MM.yyyy"),
            ["Uhrzeit"] = jetzt.ToString("HH:mm"),
            ["Agent"] = handelnder.GetCodename() ?? string.Empty,
            ["Dienstgrad"] = handelnder.GetDienstgrad() is { } dg ? DienstgradAnzeige.Name(dg) : string.Empty,
        };

        // Werte HTML-kodieren (gehen in den HTML-Body) – unbekannte Tokens unverändert lassen.
        return TokenRegex().Replace(html, m =>
        {
            var schluessel = m.Groups[1].Value;
            return ersetzungen.TryGetValue(schluessel, out var wert)
                ? System.Net.WebUtility.HtmlEncode(wert)
                : m.Value;
        });
    }

    private static async Task<(string Name, string Aktenzeichen)> AkteNameAsync(AppDbContext db, string typ, string id, CancellationToken ct)
    {
        switch (typ)
        {
            case nameof(Person):
            {
                var x = await db.Personen.Where(p => p.Id == id).Select(p => new { p.Name, p.Aktenzeichen }).FirstOrDefaultAsync(ct);
                return (x?.Name ?? string.Empty, x?.Aktenzeichen ?? string.Empty);
            }
            case nameof(Fraktion):
            {
                var x = await db.Fraktionen.Where(f => f.Id == id).Select(f => new { f.Name, f.Aktenzeichen }).FirstOrDefaultAsync(ct);
                return (x?.Name ?? string.Empty, x?.Aktenzeichen ?? string.Empty);
            }
            case nameof(Personengruppe):
            {
                var x = await db.Personengruppen.Where(g => g.Id == id).Select(g => new { g.Name, g.Aktenzeichen }).FirstOrDefaultAsync(ct);
                return (x?.Name ?? string.Empty, x?.Aktenzeichen ?? string.Empty);
            }
            case nameof(Partei):
            {
                var x = await db.Parteien.Where(p => p.Id == id).Select(p => new { p.Name, p.Aktenzeichen }).FirstOrDefaultAsync(ct);
                return (x?.Name ?? string.Empty, x?.Aktenzeichen ?? string.Empty);
            }
            case nameof(Operation):
            {
                var x = await db.Operationen.Where(o => o.Id == id).Select(o => new { Name = o.Titel, o.Aktenzeichen }).FirstOrDefaultAsync(ct);
                return (x?.Name ?? string.Empty, x?.Aktenzeichen ?? string.Empty);
            }
            case nameof(Taskforce):
            {
                var x = await db.Taskforces.Where(t => t.Id == id).Select(t => new { t.Name, t.Aktenzeichen }).FirstOrDefaultAsync(ct);
                return (x?.Name ?? string.Empty, x?.Aktenzeichen ?? string.Empty);
            }
            case nameof(Vorgang):
            {
                var x = await db.Vorgaenge.Where(v => v.Id == id).Select(v => new { Name = v.Titel, v.Aktenzeichen }).FirstOrDefaultAsync(ct);
                return (x?.Name ?? string.Empty, x?.Aktenzeichen ?? string.Empty);
            }
            case nameof(Aufgabe):
            {
                var x = await db.Aufgaben.Where(a => a.Id == id).Select(a => new { Name = a.Titel, a.Aktenzeichen }).FirstOrDefaultAsync(ct);
                return (x?.Name ?? string.Empty, x?.Aktenzeichen ?? string.Empty);
            }
            default:
                return (string.Empty, string.Empty);
        }
    }

    [GeneratedRegex(@"\{\{\s*(\w+)\s*\}\}")]
    private static partial Regex TokenRegex();
}
