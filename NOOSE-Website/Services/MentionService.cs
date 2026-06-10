using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IMentionService" />
public class MentionService(IDbContextFactory<AppDbContext> dbFactory, ISearchService search) : IMentionService
{
    private const int KandidatenProGruppe = 5;

    public async Task<IReadOnlyList<MentionSegment>> AufloesenAsync(string? text, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<MentionSegment>();
        }
        var tokens = MentionParser.Parse(text);
        if (tokens.Count == 0)
        {
            return new[] { new MentionSegment(false, text) };
        }

        var refs = tokens.Select(t => (t.Typ, t.Id)).Distinct().ToList();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var map = await AktenReferenz.AufloesenAsync(db, refs, cancellationToken);
        return Segmentieren(text, tokens, map, istFuehrung);
    }

    public async Task<IReadOnlyList<IReadOnlyList<MentionSegment>>> AufloesenVieleAsync(IReadOnlyList<string?> texte, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        // Alle Tokens aller Texte einmal sammeln und in EINER Sammelabfrage auflösen.
        var tokenProText = texte.Select(MentionParser.Parse).ToList();
        var refs = tokenProText.SelectMany(ts => ts).Select(t => (t.Typ, t.Id)).Distinct().ToList();

        Dictionary<(string, string), AktenReferenz.Aufloesung> map;
        if (refs.Count == 0)
        {
            map = new();
        }
        else
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            map = await AktenReferenz.AufloesenAsync(db, refs, cancellationToken);
        }

        var ergebnis = new List<IReadOnlyList<MentionSegment>>(texte.Count);
        for (var i = 0; i < texte.Count; i++)
        {
            ergebnis.Add(Segmentieren(texte[i] ?? string.Empty, tokenProText[i], map, istFuehrung));
        }
        return ergebnis;
    }

    private static List<MentionSegment> Segmentieren(string text, IReadOnlyList<MentionToken> tokens,
        Dictionary<(string, string), AktenReferenz.Aufloesung> map, bool istFuehrung)
    {
        if (tokens.Count == 0)
        {
            return new() { new MentionSegment(false, text) };
        }
        var segmente = new List<MentionSegment>();
        var pos = 0;
        foreach (var tok in tokens)
        {
            if (tok.Start > pos)
            {
                segmente.Add(new MentionSegment(false, text.Substring(pos, tok.Start - pos)));
            }
            if (map.TryGetValue((tok.Typ, tok.Id), out var a))
            {
                // Verschlusssache, die der Betrachter nicht sehen darf → neutraler Chip ohne Name/Link.
                segmente.Add(a.Verschluss && !istFuehrung
                    ? new MentionSegment(true, "Verschlusssache", tok.Typ, null, Verborgen: true)
                    : new MentionSegment(true, a.Anzeige, tok.Typ, a.Href, false));
            }
            else
            {
                // Ziel gelöscht/unbekannt.
                segmente.Add(new MentionSegment(true, "(nicht verfügbar)", tok.Typ, null, false));
            }
            pos = tok.Start + tok.Laenge;
        }
        if (pos < text.Length)
        {
            segmente.Add(new MentionSegment(false, text.Substring(pos)));
        }
        return segmente;
    }

    public async Task<List<MentionTreffer>> KandidatenAsync(string? text, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        var s = text?.Trim();
        if (string.IsNullOrEmpty(s))
        {
            return new();
        }

        var treffer = new List<MentionTreffer>();

        // 1) Akten (Person/Fraktion/Gruppe/Partei/Operation/Taskforce) über die Schnellsuche – bereits VS-gefiltert.
        var akten = await search.SchnellsucheAsync(s, istFuehrung, 8, cancellationToken);
        treffer.AddRange(akten.Select(a => new MentionTreffer(a.Kategorie, a.ZielId, a.Name, a.Aktenzeichen)));

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // 2) Agenten – Codename (Klarname nur für die Führung, sonst wäre die Suche ein Orakel auf das Geheimfeld).
        var darfKlarname = istFuehrung; // DarfKlarnameSehen() == IstFuehrung()
        var agenten = await db.Users
            .Where(u => u.Status == AgentStatus.Aktiv
                && (u.Codename.Contains(s) || (darfKlarname && u.Klarname != null && u.Klarname.Contains(s))))
            .OrderBy(u => u.Codename).Take(KandidatenProGruppe)
            .Select(u => new { u.Id, u.Codename, u.Klarname }).ToListAsync(cancellationToken);
        treffer.AddRange(agenten.Select(a => new MentionTreffer(nameof(Agent), a.Id,
            string.IsNullOrWhiteSpace(a.Codename) ? "(unbenannter Agent)" : a.Codename,
            darfKlarname ? a.Klarname : null)));

        // 3) Quellen – Titel-Suche; Sichtbarkeit über die Eltern-Akte (Verschlusssache + Existenz).
        var quellenRoh = await db.Quellen.Where(q => q.Titel.Contains(s))
            .OrderByDescending(q => q.GeaendertAm ?? q.ErstelltAm).Take(20)
            .Select(q => new { q.Id, q.Titel, q.EntitaetTyp, q.EntitaetId }).ToListAsync(cancellationToken);
        if (quellenRoh.Count > 0)
        {
            var elternRefs = quellenRoh.Select(q => (q.EntitaetTyp, q.EntitaetId)).Distinct().ToList();
            var elternMap = await AktenReferenz.AufloesenAsync(db, elternRefs, cancellationToken);
            var anzahl = 0;
            foreach (var q in quellenRoh)
            {
                if (!elternMap.TryGetValue((q.EntitaetTyp, q.EntitaetId), out var e) || (e.Verschluss && !istFuehrung))
                {
                    continue; // Eltern-Akte fehlt/Papierkorb oder Verschlusssache ohne Berechtigung.
                }
                treffer.Add(new MentionTreffer(nameof(Quelle), q.Id, string.IsNullOrWhiteSpace(q.Titel) ? "Quelle" : q.Titel, e.Anzeige));
                if (++anzahl >= KandidatenProGruppe)
                {
                    break;
                }
            }
        }

        return treffer;
    }
}
