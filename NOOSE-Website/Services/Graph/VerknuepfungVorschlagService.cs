using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Models.Graph;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IVerknuepfungVorschlagService" />
public class VerknuepfungVorschlagService(IDbContextFactory<AppDbContext> dbFactory) : IVerknuepfungVorschlagService
{
    private const int MaxVorschlaege = 12;

    public async Task<List<VerknuepfungVorschlag>> GetVorschlaegeAsync(string entitaetTyp, string entitaetId, ClaimsPrincipal betrachter, CancellationToken cancellationToken = default)
    {
        // Block A: nur Personen-Akten liefern Vorschläge (die Signale sind personenzentriert).
        if (entitaetTyp != nameof(Person))
        {
            return new();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var istFuehrung = betrachter.IstFuehrung();

        // Kandidaten-Personen → Menge der zutreffenden Begründungen.
        var kandidaten = new Dictionary<string, HashSet<string>>();
        void Hinzufuegen(string personId, string grund)
        {
            if (string.IsNullOrEmpty(personId) || personId == entitaetId)
            {
                return;
            }
            if (!kandidaten.TryGetValue(personId, out var gruende))
            {
                gruende = new();
                kandidaten[personId] = gruende;
            }
            gruende.Add(grund);
        }

        // ---- Signal 1: gleiche Telefonnummer (normalisiert auf Ziffern) ----
        var eigeneNummern = await db.PersonTelefone.Where(t => t.PersonId == entitaetId)
            .Select(t => t.Nummer).ToListAsync(cancellationToken);
        var eigeneNummernNorm = eigeneNummern.Select(Normalisiere).Where(n => n.Length > 0).ToHashSet();
        if (eigeneNummernNorm.Count > 0)
        {
            var andere = await db.PersonTelefone.Where(t => t.PersonId != entitaetId)
                .Select(t => new { t.PersonId, t.Nummer }).ToListAsync(cancellationToken);
            foreach (var t in andere)
            {
                if (eigeneNummernNorm.Contains(Normalisiere(t.Nummer)))
                {
                    Hinzufuegen(t.PersonId, $"gleiche Telefonnummer ({t.Nummer})");
                }
            }
        }

        // ---- Signal 2: gleiche Fraktion ----
        var fraktionIds = await db.FraktionMitglieder.Where(m => m.PersonId == entitaetId)
            .Select(m => m.FraktionId).Distinct().ToListAsync(cancellationToken);
        if (fraktionIds.Count > 0)
        {
            var namen = await db.Fraktionen.Where(f => fraktionIds.Contains(f.Id))
                .Select(f => new { f.Id, f.Name }).ToDictionaryAsync(f => f.Id, f => f.Name, cancellationToken);
            var mitglieder = await db.FraktionMitglieder
                .Where(m => m.PersonId != entitaetId && fraktionIds.Contains(m.FraktionId))
                .Select(m => new { m.PersonId, m.FraktionId }).ToListAsync(cancellationToken);
            foreach (var m in mitglieder)
            {
                Hinzufuegen(m.PersonId, $"gleiche Fraktion: {namen.GetValueOrDefault(m.FraktionId, "?")}");
            }
        }

        // ---- Signal 3: gleiche Personengruppe ----
        var gruppenIds = await db.PersonengruppeMitglieder.Where(m => m.PersonId == entitaetId)
            .Select(m => m.PersonengruppeId).Distinct().ToListAsync(cancellationToken);
        if (gruppenIds.Count > 0)
        {
            var namen = await db.Personengruppen.Where(g => gruppenIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Name }).ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken);
            var mitglieder = await db.PersonengruppeMitglieder
                .Where(m => m.PersonId != entitaetId && gruppenIds.Contains(m.PersonengruppeId))
                .Select(m => new { m.PersonId, m.PersonengruppeId }).ToListAsync(cancellationToken);
            foreach (var m in mitglieder)
            {
                Hinzufuegen(m.PersonId, $"gleiche Gruppe: {namen.GetValueOrDefault(m.PersonengruppeId, "?")}");
            }
        }

        // ---- Signal 4: gemeinsamer Tag ----
        var tagIds = await db.TagZuordnungen
            .Where(z => z.EntitaetTyp == nameof(Person) && z.EntitaetId == entitaetId)
            .Select(z => z.TagId).Distinct().ToListAsync(cancellationToken);
        if (tagIds.Count > 0)
        {
            var tagNamen = await db.Tags.Where(t => tagIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Name }).ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);
            var zuordnungen = await db.TagZuordnungen
                .Where(z => z.EntitaetTyp == nameof(Person) && z.EntitaetId != entitaetId && tagIds.Contains(z.TagId))
                .Select(z => new { z.EntitaetId, z.TagId }).ToListAsync(cancellationToken);
            foreach (var z in zuordnungen)
            {
                Hinzufuegen(z.EntitaetId, $"gemeinsamer Tag: {tagNamen.GetValueOrDefault(z.TagId, "?")}");
            }
        }

        // ---- Signal 5: gemeinsame Verknüpfung (gleicher Nachbar im Verknüpfungs-Graph) ----
        // Bereits (manuell) verknüpfte/bezogene Personen werden zugleich für den Ausschluss gesammelt.
        var meKey = $"{nameof(Person)}:{entitaetId}";
        var bereitsVerknuepft = new HashSet<string>();
        var alleVk = await db.Verknuepfungen
            .Select(v => new { v.VonTyp, v.VonId, v.NachTyp, v.NachId })
            .ToListAsync(cancellationToken);
        var nachbarn = new HashSet<string>();
        foreach (var v in alleVk)
        {
            var von = $"{v.VonTyp}:{v.VonId}";
            var nach = $"{v.NachTyp}:{v.NachId}";
            if (von == meKey)
            {
                nachbarn.Add(nach);
                if (v.NachTyp == nameof(Person))
                {
                    bereitsVerknuepft.Add(v.NachId);
                }
            }
            else if (nach == meKey)
            {
                nachbarn.Add(von);
                if (v.VonTyp == nameof(Person))
                {
                    bereitsVerknuepft.Add(v.VonId);
                }
            }
        }
        foreach (var v in alleVk)
        {
            var von = $"{v.VonTyp}:{v.VonId}";
            var nach = $"{v.NachTyp}:{v.NachId}";
            if (nachbarn.Contains(von) && v.NachTyp == nameof(Person) && nach != meKey)
            {
                Hinzufuegen(v.NachId, "gemeinsame Verknüpfung");
            }
            else if (nachbarn.Contains(nach) && v.VonTyp == nameof(Person) && von != meKey)
            {
                Hinzufuegen(v.VonId, "gemeinsame Verknüpfung");
            }
        }

        // Bereits typisierte Person-Beziehungen ebenfalls vom Vorschlag ausschließen.
        var bezogene = await db.PersonBeziehungen
            .Where(b => b.PersonAId == entitaetId || b.PersonBId == entitaetId)
            .Select(b => new { b.PersonAId, b.PersonBId }).ToListAsync(cancellationToken);
        foreach (var b in bezogene)
        {
            bereitsVerknuepft.Add(b.PersonAId == entitaetId ? b.PersonBId : b.PersonAId);
        }

        // Kandidaten ohne bereits bestehende Verknüpfung/Beziehung.
        var ids = kandidaten.Keys.Where(p => !bereitsVerknuepft.Contains(p)).ToList();
        if (ids.Count == 0)
        {
            return new();
        }

        // Auflösen + Sichtbarkeit (Verschlusssache nur für Führung; Papierkorb via globalem Filter).
        var personen = await db.Personen.Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.Aktenzeichen, p.IstVerschlusssache })
            .ToListAsync(cancellationToken);

        var ergebnis = new List<VerknuepfungVorschlag>();
        foreach (var p in personen)
        {
            if (p.IstVerschlusssache && !istFuehrung)
            {
                continue;
            }
            var gruende = kandidaten[p.Id];
            ergebnis.Add(new VerknuepfungVorschlag(
                nameof(Person), p.Id, p.Name, p.Aktenzeichen, $"/personen/{p.Id}",
                string.Join(" · ", gruende), gruende.Count));
        }

        return ergebnis
            .OrderByDescending(v => v.Staerke)
            .ThenBy(v => v.Bezeichnung)
            .Take(MaxVorschlaege)
            .ToList();
    }

    /// <summary>Reduziert eine Telefonnummer auf ihre Ziffern (toleriert Formatierungen/Leerzeichen).</summary>
    private static string Normalisiere(string? nummer)
        => string.IsNullOrEmpty(nummer) ? string.Empty : new string(nummer.Where(char.IsDigit).ToArray());
}
