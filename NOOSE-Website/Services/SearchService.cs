using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Aufgaben;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Operationen;
using NOOSE_Website.Data.Entities.Parteien;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Vorgaenge;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ISearchService" />
public class SearchService(IDbContextFactory<AppDbContext> dbFactory) : ISearchService
{
    private const int MaxProKategorie = 50;

    /// <summary>Obergrenze der in-memory geprüften Fuzzy-Kandidaten je Kategorie (Schutz vor Last bei großen Datenmengen).</summary>
    private const int FuzzyKandidatenMax = 2000;

    public async Task<List<SuchErgebnisGruppe>> SuchenAsync(SuchKriterien kriterien, bool istFuehrung, string? meId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var s = kriterien.Text?.Trim();
        var hatText = !string.IsNullOrEmpty(s);
        var tagIds = kriterien.TagIds ?? new();
        var hatTags = tagIds.Count > 0;
        var max = kriterien.MaxModus;

        // Bewusst KEIN Früh-Ausstieg bei leerem Text/leeren Tags: ohne Filter sollen alle (sichtbaren)
        // Personen erscheinen (Durchblättern). Die Personen-Query unten lässt dann einfach das Text-Where
        // weg; die reinen Text-Kategorien (Doks/Quellen/Kommentare) bleiben mangels Suchtext leer.

        var kategorien = kriterien.Kategorien is { Count: > 0 } ? kriterien.Kategorien.ToHashSet() : null;
        bool Aktiv(string kat) => kategorien is null || kategorien.Contains(kat);

        // Im Max-Modus werden die Inhalts-Kategorien (Doks/Quellen/Kommentare) immer mitdurchsucht,
        // unabhängig davon, ob ihr Häkchen gesetzt ist – eine einzige Wahrheitsquelle für die Erzwingung.
        bool InhaltAktiv(string kat) => max || Aktiv(kat);

        // Suchwörter nur einmal zerlegen (für den in-memory Fuzzy-Pass).
        var suchworte = kriterien.Fuzzy && hatText
            ? TextAehnlichkeit.Tokens(s)
            : (IReadOnlyList<string>)Array.Empty<string>();
        bool FuzzyAktiv(int substringTreffer) => kriterien.Fuzzy && hatText && substringTreffer < MaxProKategorie;

        var gruppen = new List<SuchErgebnisGruppe>();

        // ---- Personen (Name/Aktenzeichen/Beschreibung/Aliase; Max zusätzlich Steckbrief-Unterdaten) ----
        if (Aktiv(nameof(Person)))
        {
            var q = db.Personen.Where(p => istFuehrung || !p.IstVerschlusssache);
            if (hatText)
            {
                q = q.Where(p => p.Name.Contains(s!) || p.Aktenzeichen.Contains(s!)
                    || (p.Beschreibung != null && p.Beschreibung.Contains(s!))
                    || p.Aliase.Any(a => a.Aliasname.Contains(s!))
                    || (max && (
                           p.Telefonnummern.Any(t => t.Nummer.Contains(s!) || (t.Bezeichnung != null && t.Bezeichnung.Contains(s!)))
                        || p.Fahrzeuge.Any(f => f.Bezeichnung.Contains(s!) || (f.Kennzeichen != null && f.Kennzeichen.Contains(s!)))
                        || p.Orte.Any(o => o.Text.Contains(s!) || (o.Notiz != null && o.Notiz.Contains(s!)))
                        || p.Waffen.Any(w => w.Text.Contains(s!)))));
            }
            if (hatTags)
            {
                q = q.Where(p => db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Person) && z.EntitaetId == p.Id && tagIds.Contains(z.TagId)));
            }
            var treffer = await q.OrderBy(p => p.Name).Take(MaxProKategorie)
                .Select(p => new SuchTreffer(nameof(Person), p.Id, p.Name,
                    p.Beschreibung ?? string.Empty, p.Aktenzeichen))
                .ToListAsync(cancellationToken);

            if (FuzzyAktiv(treffer.Count))
            {
                var basis = db.Personen.Where(p => istFuehrung || !p.IstVerschlusssache);
                if (hatTags)
                {
                    basis = basis.Where(p => db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Person) && z.EntitaetId == p.Id && tagIds.Contains(z.TagId)));
                }
                var roh = await basis.OrderByDescending(p => p.GeaendertAm ?? p.ErstelltAm).Take(FuzzyKandidatenMax)
                    .Select(p => new { p.Id, p.Name, p.Aktenzeichen, p.Beschreibung })
                    .ToListAsync(cancellationToken);
                // Aliase separat als flache Abfrage über die Kind-Tabelle laden (WHERE PersonId IN …).
                // Bewusst KEIN SelectMany über die Navigation und KEINE Collection-Projektion mit .ToList():
                // beides erzeugt auf MySQL/MariaDB ein nicht übersetzbares CROSS APPLY bzw. LATERAL.
                var ids = roh.Select(x => x.Id).ToList();
                var aliasNachPerson = (await db.PersonAliase
                        .Where(a => ids.Contains(a.PersonId))
                        .Select(a => new { a.PersonId, a.Aliasname })
                        .ToListAsync(cancellationToken))
                    .GroupBy(a => a.PersonId)
                    .ToDictionary(g => g.Key, g => g.Select(a => a.Aliasname).ToList());
                var kandidaten = roh.Select(x =>
                {
                    var aliase = aliasNachPerson.TryGetValue(x.Id, out var liste) ? liste : new List<string>();
                    return new FuzzyKandidat(x.Id, x.Name, x.Aktenzeichen, x.Beschreibung ?? string.Empty,
                        max
                            ? TextAehnlichkeit.Tokens(new[] { x.Name, x.Aktenzeichen, x.Beschreibung }.Concat(aliase).ToArray())
                            : TextAehnlichkeit.Tokens(new[] { x.Name, x.Aktenzeichen }.Concat(aliase).ToArray()));
                });
                treffer = FuzzyErgaenzen(nameof(Person), treffer, suchworte, kandidaten);
            }

            if (treffer.Count > 0)
            {
                gruppen.Add(new SuchErgebnisGruppe(nameof(Person), "Personen", treffer));
            }
        }

        // ---- Fraktionen (Name/Aktenzeichen/Art/Beschreibung/Ziele; Max zusätzlich Anwesen/Funk/Darkchat/Ausstellungszeiten) ----
        if (Aktiv(nameof(Fraktion)))
        {
            var q = db.Fraktionen.Where(f => istFuehrung || !f.IstVerschlusssache);
            if (hatText)
            {
                q = q.Where(f => f.Name.Contains(s!) || f.Aktenzeichen.Contains(s!)
                    || (f.Art != null && f.Art.Contains(s!))
                    || (f.Beschreibung != null && f.Beschreibung.Contains(s!))
                    || (f.Ziele != null && f.Ziele.Contains(s!))
                    || (max && (
                           (f.Anwesen != null && f.Anwesen.Contains(s!))
                        || (f.Funk != null && f.Funk.Contains(s!))
                        || (f.Darkchat != null && f.Darkchat.Contains(s!))
                        || (f.Ausstellungszeiten != null && f.Ausstellungszeiten.Contains(s!)))));
            }
            if (hatTags)
            {
                q = q.Where(f => db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Fraktion) && z.EntitaetId == f.Id && tagIds.Contains(z.TagId)));
            }
            var treffer = await q.OrderBy(f => f.Name).Take(MaxProKategorie)
                .Select(f => new SuchTreffer(nameof(Fraktion), f.Id, f.Name, f.Art ?? string.Empty, f.Aktenzeichen))
                .ToListAsync(cancellationToken);

            if (FuzzyAktiv(treffer.Count))
            {
                var basis = db.Fraktionen.Where(f => istFuehrung || !f.IstVerschlusssache);
                if (hatTags)
                {
                    basis = basis.Where(f => db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Fraktion) && z.EntitaetId == f.Id && tagIds.Contains(z.TagId)));
                }
                var roh = await basis.OrderByDescending(f => f.GeaendertAm ?? f.ErstelltAm).Take(FuzzyKandidatenMax)
                    .Select(f => new { f.Id, f.Name, f.Aktenzeichen, f.Art, f.Beschreibung, f.Ziele, f.Anwesen, f.Funk, f.Darkchat, f.Ausstellungszeiten })
                    .ToListAsync(cancellationToken);
                var kandidaten = roh.Select(x => new FuzzyKandidat(x.Id, x.Name, x.Aktenzeichen, x.Art ?? string.Empty,
                    max
                        ? TextAehnlichkeit.Tokens(x.Name, x.Aktenzeichen, x.Art, x.Beschreibung, x.Ziele, x.Anwesen, x.Funk, x.Darkchat, x.Ausstellungszeiten)
                        : TextAehnlichkeit.Tokens(x.Name, x.Aktenzeichen)));
                treffer = FuzzyErgaenzen(nameof(Fraktion), treffer, suchworte, kandidaten);
            }

            if (treffer.Count > 0)
            {
                gruppen.Add(new SuchErgebnisGruppe(nameof(Fraktion), "Fraktionen", treffer));
            }
        }

        // ---- Personengruppen (Name/Aktenzeichen/Beschreibung/Ziele/Art; Ziele jetzt analog Fraktion/Partei) ----
        if (Aktiv(nameof(Personengruppe)))
        {
            var q = db.Personengruppen.Where(g => istFuehrung || !g.IstVerschlusssache);
            if (hatText)
            {
                // Auch nach Kategorie-Namen (z. B. „Persönlichkeit", „Person of Interest") suchbar.
                var passendeArten = GruppenArtAnzeige.Alle
                    .Where(a => GruppenArtAnzeige.Name(a).Contains(s!, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                q = q.Where(g => g.Name.Contains(s!) || g.Aktenzeichen.Contains(s!)
                    || (g.Beschreibung != null && g.Beschreibung.Contains(s!))
                    || (g.Ziele != null && g.Ziele.Contains(s!))
                    || passendeArten.Contains(g.Art));
            }
            if (hatTags)
            {
                q = q.Where(g => db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Personengruppe) && z.EntitaetId == g.Id && tagIds.Contains(z.TagId)));
            }
            var treffer = await q.OrderBy(g => g.Name).Take(MaxProKategorie)
                .Select(g => new SuchTreffer(nameof(Personengruppe), g.Id, g.Name, g.Beschreibung ?? string.Empty, g.Aktenzeichen))
                .ToListAsync(cancellationToken);

            if (FuzzyAktiv(treffer.Count))
            {
                var basis = db.Personengruppen.Where(g => istFuehrung || !g.IstVerschlusssache);
                if (hatTags)
                {
                    basis = basis.Where(g => db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Personengruppe) && z.EntitaetId == g.Id && tagIds.Contains(z.TagId)));
                }
                var roh = await basis.OrderByDescending(g => g.GeaendertAm ?? g.ErstelltAm).Take(FuzzyKandidatenMax)
                    .Select(g => new { g.Id, g.Name, g.Aktenzeichen, g.Beschreibung, g.Ziele })
                    .ToListAsync(cancellationToken);
                var kandidaten = roh.Select(x => new FuzzyKandidat(x.Id, x.Name, x.Aktenzeichen, x.Beschreibung ?? string.Empty,
                    max
                        ? TextAehnlichkeit.Tokens(x.Name, x.Aktenzeichen, x.Beschreibung, x.Ziele)
                        : TextAehnlichkeit.Tokens(x.Name, x.Aktenzeichen)));
                treffer = FuzzyErgaenzen(nameof(Personengruppe), treffer, suchworte, kandidaten);
            }

            if (treffer.Count > 0)
            {
                gruppen.Add(new SuchErgebnisGruppe(nameof(Personengruppe), "Personengruppen", treffer));
            }
        }

        // ---- Parteien (Name/Aktenzeichen/Beschreibung/Ziele/Bemerkungen) ----
        if (Aktiv(nameof(Partei)))
        {
            var q = db.Parteien.Where(p => istFuehrung || !p.IstVerschlusssache);
            if (hatText)
            {
                q = q.Where(p => p.Name.Contains(s!) || p.Aktenzeichen.Contains(s!)
                    || (p.Beschreibung != null && p.Beschreibung.Contains(s!))
                    || (p.Ziele != null && p.Ziele.Contains(s!))
                    || (p.Bemerkungen != null && p.Bemerkungen.Contains(s!)));
            }
            if (hatTags)
            {
                q = q.Where(p => db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Partei) && z.EntitaetId == p.Id && tagIds.Contains(z.TagId)));
            }
            var treffer = await q.OrderBy(p => p.Name).Take(MaxProKategorie)
                .Select(p => new SuchTreffer(nameof(Partei), p.Id, p.Name, p.Beschreibung ?? string.Empty, p.Aktenzeichen))
                .ToListAsync(cancellationToken);

            if (FuzzyAktiv(treffer.Count))
            {
                var basis = db.Parteien.Where(p => istFuehrung || !p.IstVerschlusssache);
                if (hatTags)
                {
                    basis = basis.Where(p => db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Partei) && z.EntitaetId == p.Id && tagIds.Contains(z.TagId)));
                }
                var roh = await basis.OrderByDescending(p => p.GeaendertAm ?? p.ErstelltAm).Take(FuzzyKandidatenMax)
                    .Select(p => new { p.Id, p.Name, p.Aktenzeichen, p.Beschreibung, p.Ziele, p.Bemerkungen })
                    .ToListAsync(cancellationToken);
                var kandidaten = roh.Select(x => new FuzzyKandidat(x.Id, x.Name, x.Aktenzeichen, x.Beschreibung ?? string.Empty,
                    max
                        ? TextAehnlichkeit.Tokens(x.Name, x.Aktenzeichen, x.Beschreibung, x.Ziele, x.Bemerkungen)
                        : TextAehnlichkeit.Tokens(x.Name, x.Aktenzeichen)));
                treffer = FuzzyErgaenzen(nameof(Partei), treffer, suchworte, kandidaten);
            }

            if (treffer.Count > 0)
            {
                gruppen.Add(new SuchErgebnisGruppe(nameof(Partei), "Parteien", treffer));
            }
        }

        // ---- Operationen (Titel/Aktenzeichen/Ablauf/Ergebnis/Ort/Typ/Bemerkungen) ----
        if (Aktiv(nameof(Operation)))
        {
            var q = db.Operationen.Where(o => istFuehrung || !o.IstVerschlusssache);
            if (hatText)
            {
                q = q.Where(o => o.Titel.Contains(s!) || o.Aktenzeichen.Contains(s!)
                    || (o.Ablauf != null && o.Ablauf.Contains(s!))
                    || (o.Ergebnis != null && o.Ergebnis.Contains(s!))
                    || (o.Ort != null && o.Ort.Contains(s!))
                    || (o.Typ != null && o.Typ.Contains(s!))
                    || (o.Bemerkungen != null && o.Bemerkungen.Contains(s!)));
            }
            if (hatTags)
            {
                q = q.Where(o => db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Operation) && z.EntitaetId == o.Id && tagIds.Contains(z.TagId)));
            }
            var treffer = await q.OrderBy(o => o.Titel).Take(MaxProKategorie)
                .Select(o => new SuchTreffer(nameof(Operation), o.Id, o.Titel, o.Ablauf ?? o.Typ ?? string.Empty, o.Aktenzeichen))
                .ToListAsync(cancellationToken);

            if (FuzzyAktiv(treffer.Count))
            {
                var basis = db.Operationen.Where(o => istFuehrung || !o.IstVerschlusssache);
                if (hatTags)
                {
                    basis = basis.Where(o => db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Operation) && z.EntitaetId == o.Id && tagIds.Contains(z.TagId)));
                }
                var roh = await basis.OrderByDescending(o => o.GeaendertAm ?? o.ErstelltAm).Take(FuzzyKandidatenMax)
                    .Select(o => new { o.Id, o.Titel, o.Aktenzeichen, o.Typ, o.Ort, o.Ablauf, o.Ergebnis, o.Bemerkungen })
                    .ToListAsync(cancellationToken);
                var kandidaten = roh.Select(x => new FuzzyKandidat(x.Id, x.Titel, x.Aktenzeichen, x.Ablauf ?? x.Typ ?? string.Empty,
                    max
                        ? TextAehnlichkeit.Tokens(x.Titel, x.Aktenzeichen, x.Typ, x.Ort, x.Ablauf, x.Ergebnis, x.Bemerkungen)
                        : TextAehnlichkeit.Tokens(x.Titel, x.Aktenzeichen)));
                treffer = FuzzyErgaenzen(nameof(Operation), treffer, suchworte, kandidaten);
            }

            if (treffer.Count > 0)
            {
                gruppen.Add(new SuchErgebnisGruppe(nameof(Operation), "Operationen", treffer));
            }
        }

        // ---- Taskforces (Name/Aktenzeichen/Zweck/Bemerkungen) ----
        if (Aktiv(nameof(Taskforce)))
        {
            var q = db.Taskforces.NurSichtbare(db, istFuehrung, meId);
            if (hatText)
            {
                q = q.Where(t => t.Name.Contains(s!) || t.Aktenzeichen.Contains(s!)
                    || (t.Zweck != null && t.Zweck.Contains(s!))
                    || (t.Bemerkungen != null && t.Bemerkungen.Contains(s!)));
            }
            if (hatTags)
            {
                q = q.Where(t => db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Taskforce) && z.EntitaetId == t.Id && tagIds.Contains(z.TagId)));
            }
            var treffer = await q.OrderBy(t => t.Name).Take(MaxProKategorie)
                .Select(t => new SuchTreffer(nameof(Taskforce), t.Id, t.Name, t.Zweck ?? string.Empty, t.Aktenzeichen))
                .ToListAsync(cancellationToken);

            if (FuzzyAktiv(treffer.Count))
            {
                var basis = db.Taskforces.NurSichtbare(db, istFuehrung, meId);
                if (hatTags)
                {
                    basis = basis.Where(t => db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Taskforce) && z.EntitaetId == t.Id && tagIds.Contains(z.TagId)));
                }
                var roh = await basis.OrderByDescending(t => t.GeaendertAm ?? t.ErstelltAm).Take(FuzzyKandidatenMax)
                    .Select(t => new { t.Id, t.Name, t.Aktenzeichen, t.Zweck, t.Bemerkungen })
                    .ToListAsync(cancellationToken);
                var kandidaten = roh.Select(x => new FuzzyKandidat(x.Id, x.Name, x.Aktenzeichen, x.Zweck ?? string.Empty,
                    max
                        ? TextAehnlichkeit.Tokens(x.Name, x.Aktenzeichen, x.Zweck, x.Bemerkungen)
                        : TextAehnlichkeit.Tokens(x.Name, x.Aktenzeichen)));
                treffer = FuzzyErgaenzen(nameof(Taskforce), treffer, suchworte, kandidaten);
            }

            if (treffer.Count > 0)
            {
                gruppen.Add(new SuchErgebnisGruppe(nameof(Taskforce), "Taskforces", treffer));
            }
        }

        // ---- Vorgänge/Fälle (Titel/Aktenzeichen/Typ/Beschreibung/Zusammenfassung/Abschlussvermerk) ----
        if (Aktiv(nameof(Vorgang)))
        {
            var q = db.Vorgaenge.Where(v => istFuehrung || !v.IstVerschlusssache);
            if (hatText)
            {
                q = q.Where(v => v.Titel.Contains(s!) || v.Aktenzeichen.Contains(s!)
                    || (v.Typ != null && v.Typ.Contains(s!))
                    || (v.Beschreibung != null && v.Beschreibung.Contains(s!))
                    || (v.Zusammenfassung != null && v.Zusammenfassung.Contains(s!))
                    || (v.Abschlussvermerk != null && v.Abschlussvermerk.Contains(s!)));
            }
            if (hatTags)
            {
                q = q.Where(v => db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Vorgang) && z.EntitaetId == v.Id && tagIds.Contains(z.TagId)));
            }
            var treffer = await q.OrderBy(v => v.Titel).Take(MaxProKategorie)
                .Select(v => new SuchTreffer(nameof(Vorgang), v.Id, v.Titel, v.Beschreibung ?? v.Typ ?? string.Empty, v.Aktenzeichen))
                .ToListAsync(cancellationToken);

            if (FuzzyAktiv(treffer.Count))
            {
                var basis = db.Vorgaenge.Where(v => istFuehrung || !v.IstVerschlusssache);
                if (hatTags)
                {
                    basis = basis.Where(v => db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Vorgang) && z.EntitaetId == v.Id && tagIds.Contains(z.TagId)));
                }
                var roh = await basis.OrderByDescending(v => v.GeaendertAm ?? v.ErstelltAm).Take(FuzzyKandidatenMax)
                    .Select(v => new { v.Id, v.Titel, v.Aktenzeichen, v.Typ, v.Beschreibung, v.Zusammenfassung, v.Abschlussvermerk })
                    .ToListAsync(cancellationToken);
                var kandidaten = roh.Select(x => new FuzzyKandidat(x.Id, x.Titel, x.Aktenzeichen, x.Beschreibung ?? x.Typ ?? string.Empty,
                    max
                        ? TextAehnlichkeit.Tokens(x.Titel, x.Aktenzeichen, x.Typ, x.Beschreibung, x.Zusammenfassung, x.Abschlussvermerk)
                        : TextAehnlichkeit.Tokens(x.Titel, x.Aktenzeichen)));
                treffer = FuzzyErgaenzen(nameof(Vorgang), treffer, suchworte, kandidaten);
            }

            if (treffer.Count > 0)
            {
                gruppen.Add(new SuchErgebnisGruppe(nameof(Vorgang), "Vorgänge", treffer));
            }
        }

        // ---- Aufgaben (Titel/Aktenzeichen/Beschreibung; eingeschränkte nur für Beteiligte/Aufsicht) ----
        if (Aktiv(nameof(Aufgabe)))
        {
            var q = db.Aufgaben.NurSichtbare(db, istFuehrung, meId);
            if (hatText)
            {
                q = q.Where(a => a.Titel.Contains(s!) || a.Aktenzeichen.Contains(s!)
                    || (a.Beschreibung != null && a.Beschreibung.Contains(s!)));
            }
            if (hatTags)
            {
                q = q.Where(a => db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Aufgabe) && z.EntitaetId == a.Id && tagIds.Contains(z.TagId)));
            }
            var treffer = await q.OrderBy(a => a.Titel).Take(MaxProKategorie)
                .Select(a => new SuchTreffer(nameof(Aufgabe), a.Id, a.Titel, a.Beschreibung ?? string.Empty, a.Aktenzeichen))
                .ToListAsync(cancellationToken);

            if (FuzzyAktiv(treffer.Count))
            {
                var basis = db.Aufgaben.NurSichtbare(db, istFuehrung, meId);
                if (hatTags)
                {
                    basis = basis.Where(a => db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Aufgabe) && z.EntitaetId == a.Id && tagIds.Contains(z.TagId)));
                }
                var roh = await basis.OrderByDescending(a => a.GeaendertAm ?? a.ErstelltAm).Take(FuzzyKandidatenMax)
                    .Select(a => new { a.Id, a.Titel, a.Aktenzeichen, a.Beschreibung })
                    .ToListAsync(cancellationToken);
                var kandidaten = roh.Select(x => new FuzzyKandidat(x.Id, x.Titel, x.Aktenzeichen, x.Beschreibung ?? string.Empty,
                    max
                        ? TextAehnlichkeit.Tokens(x.Titel, x.Aktenzeichen, x.Beschreibung)
                        : TextAehnlichkeit.Tokens(x.Titel, x.Aktenzeichen)));
                treffer = FuzzyErgaenzen(nameof(Aufgabe), treffer, suchworte, kandidaten);
            }

            if (treffer.Count > 0)
            {
                gruppen.Add(new SuchErgebnisGruppe(nameof(Aufgabe), "Aufgaben", treffer));
            }
        }

        // Die folgenden Kategorien sind Text-Inhalte → nur bei vorhandenem Suchtext. Im Max-Modus immer aktiv.
        // Wichtig: expliziter Join auf db.Personen (NICHT Include über die soft-delete-gefilterte
        // Pflichtnavigation), sonst greift das fragile Query-Filter-/Pflichtnavigations-Zusammenspiel.
        if (hatText && InhaltAktiv(nameof(PersonDok)))
        {
            var treffer = await (
                from d in db.PersonDoks
                where (d.Grund != null && d.Grund.Contains(s!)) || (d.ErhalteneInformationen != null && d.ErhalteneInformationen.Contains(s!))
                    || (max && d.Fraktion != null && d.Fraktion.Contains(s!))
                join p in db.Personen on d.PersonId equals p.Id
                where (istFuehrung || !p.IstVerschlusssache)
                    && (!hatTags || db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Person) && z.EntitaetId == p.Id && tagIds.Contains(z.TagId)))
                orderby d.Zeitpunkt descending
                select new SuchTreffer(nameof(PersonDok), p.Id, p.Name,
                    (d.Grund ?? d.ErhalteneInformationen) ?? string.Empty, p.Aktenzeichen))
                .Take(MaxProKategorie)
                .ToListAsync(cancellationToken);
            if (treffer.Count > 0)
            {
                gruppen.Add(new SuchErgebnisGruppe(nameof(PersonDok), "Doks", treffer));
            }
        }

        if (hatText && InhaltAktiv(nameof(Quelle)))
        {
            // Quellen aller Akten-Eltern (Person/Fraktion/Gruppe) durchsuchen; Eltern + Sichtbarkeit/Tags
            // anschließend zentral auflösen, damit der Treffer auf die richtige Akte verlinkt.
            var roh = await db.Quellen
                .Where(quelle => quelle.Titel.Contains(s!) || (quelle.Beschreibung != null && quelle.Beschreibung.Contains(s!)))
                .OrderByDescending(quelle => quelle.ErstelltAm)
                .Select(quelle => new RohTreffer(quelle.EntitaetTyp, quelle.EntitaetId, quelle.Titel))
                .Take(MaxProKategorie * 4)
                .ToListAsync(cancellationToken);
            var treffer = await AkteElternTrefferAsync(db, nameof(Quelle), roh, istFuehrung, meId, hatTags, tagIds, cancellationToken);
            if (treffer.Count > 0)
            {
                gruppen.Add(new SuchErgebnisGruppe(nameof(Quelle), "Quellen", treffer));
            }
        }

        if (hatText && InhaltAktiv(nameof(Kommentar)))
        {
            var roh = await db.Kommentare
                .Where(kommentar => kommentar.Text.Contains(s!))
                .OrderByDescending(kommentar => kommentar.ErstelltAm)
                .Select(kommentar => new RohTreffer(kommentar.EntitaetTyp, kommentar.EntitaetId, kommentar.Text))
                .Take(MaxProKategorie * 4)
                .ToListAsync(cancellationToken);
            var treffer = await AkteElternTrefferAsync(db, nameof(Kommentar), roh, istFuehrung, meId, hatTags, tagIds, cancellationToken);
            if (treffer.Count > 0)
            {
                gruppen.Add(new SuchErgebnisGruppe(nameof(Kommentar), "Kommentare", treffer));
            }
        }

        return gruppen;
    }

    public async Task<List<SchnellTreffer>> SchnellsucheAsync(string text, bool istFuehrung, string? meId, int max = 8, CancellationToken cancellationToken = default)
    {
        var s = text?.Trim();
        if (string.IsNullOrEmpty(s))
        {
            return new List<SchnellTreffer>();
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var personen = await db.Personen
            .Where(p => (istFuehrung || !p.IstVerschlusssache) && (p.Name.Contains(s) || p.Aktenzeichen.Contains(s)))
            .OrderBy(p => p.Name).Take(max)
            .Select(p => new SchnellTreffer(nameof(Person), p.Id, p.Name, p.Aktenzeichen))
            .ToListAsync(cancellationToken);
        var fraktionen = await db.Fraktionen
            .Where(f => (istFuehrung || !f.IstVerschlusssache) && (f.Name.Contains(s) || f.Aktenzeichen.Contains(s)))
            .OrderBy(f => f.Name).Take(max)
            .Select(f => new SchnellTreffer(nameof(Fraktion), f.Id, f.Name, f.Aktenzeichen))
            .ToListAsync(cancellationToken);
        var gruppen = await db.Personengruppen
            .Where(g => (istFuehrung || !g.IstVerschlusssache) && (g.Name.Contains(s) || g.Aktenzeichen.Contains(s)))
            .OrderBy(g => g.Name).Take(max)
            .Select(g => new SchnellTreffer(nameof(Personengruppe), g.Id, g.Name, g.Aktenzeichen))
            .ToListAsync(cancellationToken);
        var parteien = await db.Parteien
            .Where(p => (istFuehrung || !p.IstVerschlusssache) && (p.Name.Contains(s) || p.Aktenzeichen.Contains(s)))
            .OrderBy(p => p.Name).Take(max)
            .Select(p => new SchnellTreffer(nameof(Partei), p.Id, p.Name, p.Aktenzeichen))
            .ToListAsync(cancellationToken);
        var operationen = await db.Operationen
            .Where(o => (istFuehrung || !o.IstVerschlusssache) && (o.Titel.Contains(s) || o.Aktenzeichen.Contains(s)))
            .OrderBy(o => o.Titel).Take(max)
            .Select(o => new SchnellTreffer(nameof(Operation), o.Id, o.Titel, o.Aktenzeichen))
            .ToListAsync(cancellationToken);
        var taskforces = await db.Taskforces.NurSichtbare(db, istFuehrung, meId)
            .Where(t => t.Name.Contains(s) || t.Aktenzeichen.Contains(s))
            .OrderBy(t => t.Name).Take(max)
            .Select(t => new SchnellTreffer(nameof(Taskforce), t.Id, t.Name, t.Aktenzeichen))
            .ToListAsync(cancellationToken);
        var vorgaenge = await db.Vorgaenge
            .Where(v => (istFuehrung || !v.IstVerschlusssache) && (v.Titel.Contains(s) || v.Aktenzeichen.Contains(s)))
            .OrderBy(v => v.Titel).Take(max)
            .Select(v => new SchnellTreffer(nameof(Vorgang), v.Id, v.Titel, v.Aktenzeichen))
            .ToListAsync(cancellationToken);
        var aufgaben = await db.Aufgaben.NurSichtbare(db, istFuehrung, meId)
            .Where(a => a.Titel.Contains(s) || a.Aktenzeichen.Contains(s))
            .OrderBy(a => a.Titel).Take(max)
            .Select(a => new SchnellTreffer(nameof(Aufgabe), a.Id, a.Titel, a.Aktenzeichen))
            .ToListAsync(cancellationToken);

        // Immer leicht aktive Tippfehler-Toleranz auf Identifikatoren (Name/Titel/Aktenzeichen). Pro
        // Kategorie nur, wenn der Begriff lang genug ist UND noch Platz frei ist (sonst lohnt der Scan nicht).
        var suchworte = TextAehnlichkeit.Tokens(s);
        if (suchworte.Any(w => w.Length >= TextAehnlichkeit.MinWortLaenge))
        {
            if (personen.Count < max)
            {
                var k = await db.Personen.Where(p => istFuehrung || !p.IstVerschlusssache)
                    .OrderBy(p => p.Name).Take(FuzzyKandidatenMax)
                    .Select(p => new { p.Id, p.Name, p.Aktenzeichen }).ToListAsync(cancellationToken);
                personen = SchnellFuzzy(nameof(Person), personen, suchworte, k.Select(x => (x.Id, x.Name, x.Aktenzeichen)), max);
            }
            if (fraktionen.Count < max)
            {
                var k = await db.Fraktionen.Where(f => istFuehrung || !f.IstVerschlusssache)
                    .OrderBy(f => f.Name).Take(FuzzyKandidatenMax)
                    .Select(f => new { f.Id, f.Name, f.Aktenzeichen }).ToListAsync(cancellationToken);
                fraktionen = SchnellFuzzy(nameof(Fraktion), fraktionen, suchworte, k.Select(x => (x.Id, x.Name, x.Aktenzeichen)), max);
            }
            if (gruppen.Count < max)
            {
                var k = await db.Personengruppen.Where(g => istFuehrung || !g.IstVerschlusssache)
                    .OrderBy(g => g.Name).Take(FuzzyKandidatenMax)
                    .Select(g => new { g.Id, g.Name, g.Aktenzeichen }).ToListAsync(cancellationToken);
                gruppen = SchnellFuzzy(nameof(Personengruppe), gruppen, suchworte, k.Select(x => (x.Id, x.Name, x.Aktenzeichen)), max);
            }
            if (parteien.Count < max)
            {
                var k = await db.Parteien.Where(p => istFuehrung || !p.IstVerschlusssache)
                    .OrderBy(p => p.Name).Take(FuzzyKandidatenMax)
                    .Select(p => new { p.Id, p.Name, p.Aktenzeichen }).ToListAsync(cancellationToken);
                parteien = SchnellFuzzy(nameof(Partei), parteien, suchworte, k.Select(x => (x.Id, x.Name, x.Aktenzeichen)), max);
            }
            if (operationen.Count < max)
            {
                var k = await db.Operationen.Where(o => istFuehrung || !o.IstVerschlusssache)
                    .OrderBy(o => o.Titel).Take(FuzzyKandidatenMax)
                    .Select(o => new { o.Id, Name = o.Titel, o.Aktenzeichen }).ToListAsync(cancellationToken);
                operationen = SchnellFuzzy(nameof(Operation), operationen, suchworte, k.Select(x => (x.Id, x.Name, x.Aktenzeichen)), max);
            }
            if (taskforces.Count < max)
            {
                var k = await db.Taskforces.NurSichtbare(db, istFuehrung, meId)
                    .OrderBy(t => t.Name).Take(FuzzyKandidatenMax)
                    .Select(t => new { t.Id, Name = t.Name, t.Aktenzeichen }).ToListAsync(cancellationToken);
                taskforces = SchnellFuzzy(nameof(Taskforce), taskforces, suchworte, k.Select(x => (x.Id, x.Name, x.Aktenzeichen)), max);
            }
            if (vorgaenge.Count < max)
            {
                var k = await db.Vorgaenge.Where(v => istFuehrung || !v.IstVerschlusssache)
                    .OrderBy(v => v.Titel).Take(FuzzyKandidatenMax)
                    .Select(v => new { v.Id, Name = v.Titel, v.Aktenzeichen }).ToListAsync(cancellationToken);
                vorgaenge = SchnellFuzzy(nameof(Vorgang), vorgaenge, suchworte, k.Select(x => (x.Id, x.Name, x.Aktenzeichen)), max);
            }
            if (aufgaben.Count < max)
            {
                var k = await db.Aufgaben.NurSichtbare(db, istFuehrung, meId)
                    .OrderBy(a => a.Titel).Take(FuzzyKandidatenMax)
                    .Select(a => new { a.Id, Name = a.Titel, a.Aktenzeichen }).ToListAsync(cancellationToken);
                aufgaben = SchnellFuzzy(nameof(Aufgabe), aufgaben, suchworte, k.Select(x => (x.Id, x.Name, x.Aktenzeichen)), max);
            }
        }

        // Rundlauf-Mischung, damit Personen die Trefferliste nicht verdrängen und alle Kategorien erscheinen.
        return Mischen(personen, fraktionen, gruppen, parteien, operationen, taskforces, vorgaenge, aufgaben).Take(max).ToList();
    }

    /// <summary>Mischt mehrere Trefferlisten im Rundlauf (P, F, G, …) für eine faire Verteilung.</summary>
    private static IEnumerable<SchnellTreffer> Mischen(params List<SchnellTreffer>[] listen)
    {
        for (var index = 0; ; index++)
        {
            var etwas = false;
            foreach (var liste in listen)
            {
                if (index < liste.Count)
                {
                    etwas = true;
                    yield return liste[index];
                }
            }
            if (!etwas)
            {
                yield break;
            }
        }
    }

    /// <summary>Kandidat für den in-memory Fuzzy-Pass: Anzeige-Daten + die zu vergleichenden Wörter.</summary>
    private sealed record FuzzyKandidat(string Id, string Anzeige, string Aktenzeichen, string Snippet, IReadOnlyList<string> Tokens);

    /// <summary>
    /// Hängt an eine bereits ermittelte Substring-Trefferliste die per Levenshtein ähnlichen Kandidaten
    /// an (dedupliziert gegen die vorhandenen Ziel-Ids, sortiert nach aufsteigender Editierdistanz).
    /// Substring-Treffer bleiben vorne (höhere Relevanz); Gesamtzahl auf <see cref="MaxProKategorie"/> gekappt.
    /// </summary>
    private static List<SuchTreffer> FuzzyErgaenzen(
        string kategorie, List<SuchTreffer> substring, IReadOnlyList<string> suchworte, IEnumerable<FuzzyKandidat> kandidaten)
    {
        if (suchworte.Count == 0)
        {
            return substring;
        }
        var vorhanden = substring.Select(t => t.ZielId).ToHashSet();
        var fuzzy = new List<(SuchTreffer Treffer, int Distanz)>();
        foreach (var k in kandidaten)
        {
            if (vorhanden.Contains(k.Id))
            {
                continue;
            }
            if (TextAehnlichkeit.PhraseAehnlich(suchworte, k.Tokens, out var distanz))
            {
                fuzzy.Add((new SuchTreffer(kategorie, k.Id, k.Anzeige, k.Snippet, k.Aktenzeichen), distanz));
            }
        }
        if (fuzzy.Count == 0)
        {
            return substring;
        }
        var ergebnis = new List<SuchTreffer>(substring);
        ergebnis.AddRange(fuzzy.OrderBy(f => f.Distanz).Select(f => f.Treffer));
        return ergebnis.Count > MaxProKategorie ? ergebnis.Take(MaxProKategorie).ToList() : ergebnis;
    }

    /// <summary>Leichtgewichtige Fuzzy-Ergänzung für die Schnellsuche: Identifikatoren (Name/Titel + Aktenzeichen).</summary>
    private static List<SchnellTreffer> SchnellFuzzy(
        string kategorie, List<SchnellTreffer> bereits, IReadOnlyList<string> suchworte,
        IEnumerable<(string Id, string Name, string Aktenzeichen)> kandidaten, int max)
    {
        var vorhanden = bereits.Select(t => t.ZielId).ToHashSet();
        var fuzzy = new List<(SchnellTreffer Treffer, int Distanz)>();
        foreach (var k in kandidaten)
        {
            if (vorhanden.Contains(k.Id))
            {
                continue;
            }
            if (TextAehnlichkeit.PhraseAehnlich(suchworte, TextAehnlichkeit.Tokens(k.Name, k.Aktenzeichen), out var distanz))
            {
                fuzzy.Add((new SchnellTreffer(kategorie, k.Id, k.Name, k.Aktenzeichen), distanz));
            }
        }
        if (fuzzy.Count == 0)
        {
            return bereits;
        }
        var ergebnis = new List<SchnellTreffer>(bereits);
        ergebnis.AddRange(fuzzy.OrderBy(f => f.Distanz).Take(max).Select(f => f.Treffer));
        return ergebnis;
    }

    /// <summary>Roh-Treffer eines polymorphen Inhalts (Quelle/Kommentar): Eltern-Typ/-Id + Anzeige-Schnipsel.</summary>
    private sealed record RohTreffer(string EntitaetTyp, string EntitaetId, string Schnipsel);

    /// <summary>
    /// Löst Roh-Treffer (Quellen/Kommentare) auf ihre Eltern-Akte auf: Name/Aktenzeichen je Typ
    /// (Person/Fraktion/Personengruppe), filtert Verschlusssachen (außer Führung) und – falls gefordert –
    /// nach Tags der Eltern-Akte. Reihenfolge der Roh-Treffer bleibt erhalten; auf <see cref="MaxProKategorie"/> gekürzt.
    /// </summary>
    private static async Task<List<SuchTreffer>> AkteElternTrefferAsync(
        AppDbContext db, string kategorie, List<RohTreffer> roh, bool istFuehrung, string? meId, bool hatTags, List<string> tagIds, CancellationToken cancellationToken)
    {
        if (roh.Count == 0)
        {
            return new();
        }

        var personIds = roh.Where(r => r.EntitaetTyp == nameof(Person)).Select(r => r.EntitaetId).Distinct().ToList();
        var fraktionIds = roh.Where(r => r.EntitaetTyp == nameof(Fraktion)).Select(r => r.EntitaetId).Distinct().ToList();
        var gruppenIds = roh.Where(r => r.EntitaetTyp == nameof(Personengruppe)).Select(r => r.EntitaetId).Distinct().ToList();
        var parteiIds = roh.Where(r => r.EntitaetTyp == nameof(Partei)).Select(r => r.EntitaetId).Distinct().ToList();
        var operationIds = roh.Where(r => r.EntitaetTyp == nameof(Operation)).Select(r => r.EntitaetId).Distinct().ToList();
        var taskforceIds = roh.Where(r => r.EntitaetTyp == nameof(Taskforce)).Select(r => r.EntitaetId).Distinct().ToList();
        var vorgangIds = roh.Where(r => r.EntitaetTyp == nameof(Vorgang)).Select(r => r.EntitaetId).Distinct().ToList();
        var aufgabeIds = roh.Where(r => r.EntitaetTyp == nameof(Aufgabe)).Select(r => r.EntitaetId).Distinct().ToList();

        // (Typ, Id) → (Name, Aktenzeichen, Verschlusssache). Gelöschte Akten fehlen (globaler Filter).
        var map = new Dictionary<(string, string), (string Name, string Aktenzeichen, bool Verschluss)>();
        foreach (var x in await db.Personen.Where(p => personIds.Contains(p.Id))
                     .Select(p => new { p.Id, p.Name, p.Aktenzeichen, p.IstVerschlusssache }).ToListAsync(cancellationToken))
        {
            map[(nameof(Person), x.Id)] = (x.Name, x.Aktenzeichen, x.IstVerschlusssache);
        }
        foreach (var x in await db.Fraktionen.Where(f => fraktionIds.Contains(f.Id))
                     .Select(f => new { f.Id, f.Name, f.Aktenzeichen, f.IstVerschlusssache }).ToListAsync(cancellationToken))
        {
            map[(nameof(Fraktion), x.Id)] = (x.Name, x.Aktenzeichen, x.IstVerschlusssache);
        }
        foreach (var x in await db.Personengruppen.Where(g => gruppenIds.Contains(g.Id))
                     .Select(g => new { g.Id, g.Name, g.Aktenzeichen, g.IstVerschlusssache }).ToListAsync(cancellationToken))
        {
            map[(nameof(Personengruppe), x.Id)] = (x.Name, x.Aktenzeichen, x.IstVerschlusssache);
        }
        foreach (var x in await db.Parteien.Where(p => parteiIds.Contains(p.Id))
                     .Select(p => new { p.Id, p.Name, p.Aktenzeichen, p.IstVerschlusssache }).ToListAsync(cancellationToken))
        {
            map[(nameof(Partei), x.Id)] = (x.Name, x.Aktenzeichen, x.IstVerschlusssache);
        }
        foreach (var x in await db.Operationen.Where(o => operationIds.Contains(o.Id))
                     .Select(o => new { o.Id, o.Titel, o.Aktenzeichen, o.IstVerschlusssache }).ToListAsync(cancellationToken))
        {
            map[(nameof(Operation), x.Id)] = (x.Titel, x.Aktenzeichen, x.IstVerschlusssache);
        }
        foreach (var x in await db.Taskforces.Where(t => taskforceIds.Contains(t.Id))
                     .Select(t => new { t.Id, t.Name, t.Aktenzeichen, t.IstVerschlusssache }).ToListAsync(cancellationToken))
        {
            map[(nameof(Taskforce), x.Id)] = (x.Name, x.Aktenzeichen, x.IstVerschlusssache);
        }
        foreach (var x in await db.Vorgaenge.Where(v => vorgangIds.Contains(v.Id))
                     .Select(v => new { v.Id, v.Titel, v.Aktenzeichen, v.IstVerschlusssache }).ToListAsync(cancellationToken))
        {
            map[(nameof(Vorgang), x.Id)] = (x.Titel, x.Aktenzeichen, x.IstVerschlusssache);
        }
        // Eingeschränkte Aufgaben nur für Beteiligte/Aufsicht in die Map aufnehmen – sonst werden Treffer auf
        // Kommentaren/Quellen einer eingeschränkten Aufgabe unten (fehlt in der Map → continue) ausgeblendet.
        foreach (var x in await db.Aufgaben.NurSichtbare(db, istFuehrung, meId).Where(a => aufgabeIds.Contains(a.Id))
                     .Select(a => new { a.Id, a.Titel, a.Aktenzeichen }).ToListAsync(cancellationToken))
        {
            map[(nameof(Aufgabe), x.Id)] = (x.Titel, x.Aktenzeichen, false);
        }

        // Tag-Filter: welche Eltern-Akten tragen mindestens einen der gewählten Tags?
        HashSet<(string, string)>? mitTag = null;
        if (hatTags)
        {
            mitTag = (await db.TagZuordnungen
                .Where(z => tagIds.Contains(z.TagId)
                    && ((z.EntitaetTyp == nameof(Person) && personIds.Contains(z.EntitaetId))
                     || (z.EntitaetTyp == nameof(Fraktion) && fraktionIds.Contains(z.EntitaetId))
                     || (z.EntitaetTyp == nameof(Personengruppe) && gruppenIds.Contains(z.EntitaetId))
                     || (z.EntitaetTyp == nameof(Partei) && parteiIds.Contains(z.EntitaetId))
                     || (z.EntitaetTyp == nameof(Operation) && operationIds.Contains(z.EntitaetId))
                     || (z.EntitaetTyp == nameof(Taskforce) && taskforceIds.Contains(z.EntitaetId))
                     || (z.EntitaetTyp == nameof(Vorgang) && vorgangIds.Contains(z.EntitaetId))
                     || (z.EntitaetTyp == nameof(Aufgabe) && aufgabeIds.Contains(z.EntitaetId))))
                .Select(z => new { z.EntitaetTyp, z.EntitaetId }).ToListAsync(cancellationToken))
                .Select(z => (z.EntitaetTyp, z.EntitaetId)).ToHashSet();
        }

        var ergebnis = new List<SuchTreffer>();
        foreach (var r in roh)
        {
            if (!map.TryGetValue((r.EntitaetTyp, r.EntitaetId), out var info))
            {
                continue; // Eltern-Akte gelöscht/unbekannt → ausblenden.
            }
            if (info.Verschluss && !istFuehrung)
            {
                continue;
            }
            if (hatTags && (mitTag is null || !mitTag.Contains((r.EntitaetTyp, r.EntitaetId))))
            {
                continue;
            }
            ergebnis.Add(new SuchTreffer(kategorie, r.EntitaetId, info.Name, r.Schnipsel, info.Aktenzeichen, r.EntitaetTyp));
            if (ergebnis.Count >= MaxProKategorie)
            {
                break;
            }
        }
        return ergebnis;
    }
}
