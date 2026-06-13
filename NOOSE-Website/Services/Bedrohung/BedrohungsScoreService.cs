using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Models.Bedrohung;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IBedrohungsScoreService" />
public class BedrohungsScoreService(IDbContextFactory<AppDbContext> dbFactory, IBedrohungsScoreKonfigService konfigService)
    : IBedrohungsScoreService
{
    /// <summary>Serialisierungs-Optionen für <c>BedrohungsDetailJson</c> – auch beim Deserialisieren in der UI nutzen.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Umlaute lesbar in der DB ablegen (kein ü).
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // =====================================================================================
    //  Reine FRAKTION-Berechnung (deterministisch, ohne DB) – verifizierbar gegen das „Vagos"-
    //  Beispiel in AlgoPlan.md (Inhalt ≈ 71, Score ≈ 86 / Kritisch, Konfidenz ~85 %).
    //  Alle Stellschrauben kommen aus der übergebenen Konfiguration (k); k.Default == bisherige consts.
    // =====================================================================================

    /// <summary>
    /// Berechnet Score (0–100), Daten-Konfidenz und die Aufschlüsselung einer Fraktion aus den geladenen Rohdaten.
    /// Schichten: (1) gesättigte Inhalts-Teilscores S1+S2+S3+S4 = Inhalt 0–100, (2) Einstufungs-Band-Projektion,
    /// (3) separate Konfidenz. Staatsfraktion → Score/Konfidenz <c>null</c> (ausgenommen).
    /// </summary>
    public static BedrohungsScoreErgebnis Berechne(BedrohungsScoreEingabe e, DateTime jetztUtc, BedrohungsScoreKonfiguration k)
    {
        if (e.IstStaatsfraktion)
        {
            return new BedrohungsScoreErgebnis(null, null, new BedrohungsScoreDetail
            {
                Ausgenommen = "Staatsfraktion",
                EinstufungName = EinstufungName(e.Einstufung),
                BandHinweis = "Staatsfraktion – vom Bedrohungs-Score ausgenommen.",
                BerechnetAmUtc = jetztUtc,
            });
        }

        // ---- S1: Aktivitäts- & Maßnahmen-Heat ----
        double aktHeat = 0;
        foreach (var a in e.Aktivitaeten)
        {
            aktHeat += k.ArtGewicht(a.Art) * Decay(a.Zeitpunkt, jetztUtc, k.HalbwertszeitTage);
        }
        double dokHeat = 0;
        int dokAnzahl = 0;
        foreach (var doks in e.DoksProMitglied)
        {
            double proMitglied = 0;
            foreach (var d in doks)
            {
                proMitglied += k.AusgangGewicht(d.Ausgang) * Decay(d.Zeitpunkt, jetztUtc, k.HalbwertszeitTage);
                dokAnzahl++;
            }
            dokHeat += Math.Min(k.ProMitgliedDokCap, proMitglied);
        }
        double rohS1 = aktHeat + k.DokHeatGewicht * dokHeat;
        double s1 = Saettige(rohS1, k.CapS1, k.S1Nenner);

        // ---- S2: Organisation & Reichweite (Sub-Caps summieren auf CapS2) ----
        double groesse = Math.Max(e.GeschaetzteMitgliederzahl ?? 0, e.AktiveMitgliederCount);
        double groessePkt = Saettige(groesse, k.CapGroesse, k.GroesseNenner);
        double strukturPkt = Math.Min(k.RaengeMaxPunkte, e.RaengeCount)
                           + (e.HatAktiveLeitung ? k.LeitungPunkte : 0)
                           + (e.HatAnwesen ? k.AnwesenPunkte : 0);
        double waffenPkt = Saettige(e.DistinctWaffenCount, k.CapWaffen, k.WaffenNenner);
        double infraRoh = k.DrogenrouteGewicht * e.DrogenroutenCount + e.LagerbestandCount;
        double infraPkt = Saettige(infraRoh, k.CapInfra, k.InfraNenner);
        double s2 = Math.Min(k.CapS2, groessePkt + strukturPkt + waffenPkt + infraPkt);

        // ---- S3: Konflikt & Bündnis ----
        double rohS3 = k.KonfliktGewicht * e.KonfliktCount + k.BuendnisGewicht * e.BuendnisCount;
        double s3 = Saettige(rohS3, k.CapS3, k.S3Nenner);

        // ---- S4: Netzwerk-Zentralität (manuelle Standard-Verknüpfungen; disjunkt zu S3, orthogonal zu S2) ----
        double rohS4 = e.StandardKantenGrad;
        double s4 = Saettige(rohS4, k.CapS4, k.S4Nenner);

        double inhalt = s1 + s2 + s3 + s4; // Caps 55+22+15+8 = 100 (Default)
        int sockel = BedrohungsScoreKonstanten.Sockel(e.Einstufung);
        int score = BandScore(inhalt, sockel);

        // ---- Daten-Konfidenz (separat, senkt den Score NIE) ----
        bool hatDoks = dokAnzahl > 0;
        bool hatBestaende = (e.DistinctWaffenCount + e.LagerbestandCount + e.DrogenroutenCount) > 0;
        double konf = 0.30 * Bit(e.Aktivitaeten.Count > 0 || hatDoks)
                    + 0.20 * Bit(e.AktiveMitgliederCount > 0)
                    + 0.15 * Bit(hatBestaende)
                    + 0.10 * Bit(e.GeschaetzteMitgliederzahl.HasValue)
                    + 0.10 * Bit(e.Einstufung != Einstufung.Unbekannt)
                    + 0.15 * Bit(IstFrisch(e.JuengsteErfassungUtc, jetztUtc, k.KonfidenzFrischeTage));
        int konfidenz = Prozent(konf);

        bool triage = inhalt >= k.TriageSchwelle && e.Einstufung == Einstufung.Unbekannt;
        string? triageHinweis = TriageHinweis(triage, score, konfidenz);

        var teilscores = new List<BedrohungsTeilscore>
        {
            new("Aktivitäts- & Maßnahmen-Heat", R1(rohS1), R1(s1), k.CapS1, S1Treiber(e, dokAnzahl, jetztUtc)),
            new("Organisation & Reichweite", R1(groessePkt + strukturPkt + waffenPkt + infraPkt), R1(s2), k.CapS2, S2Treiber(e, groesse)),
            new("Konflikt & Bündnis", R1(rohS3), R1(s3), k.CapS3, S3Treiber(e)),
            new("Netzwerk-Zentralität", R1(rohS4), R1(s4), k.CapS4, S4Treiber(e)),
        };

        return new BedrohungsScoreErgebnis(score, konfidenz,
            BaueDetail(teilscores, inhalt, e.Einstufung, sockel, score, konfidenz, triage, triageHinweis, jetztUtc));
    }

    // =====================================================================================
    //  Reine PERSON-Berechnung (P1–P5, Band-Projektion, separate Konfidenz). Nur person-eigene
    //  Daten → keine Zirkularität mit dem Fraktion-Score.
    // =====================================================================================

    /// <summary>Berechnet Score/Konfidenz/Aufschlüsselung einer Person. Lebensstatus on-read via <c>LebensstatusLogic.Effektiv</c>.</summary>
    public static BedrohungsScoreErgebnis BerechnePerson(PersonBedrohungsScoreEingabe e, DateTime jetztUtc, BedrohungsScoreKonfiguration k)
    {
        // ---- P1: Maßnahmen-Heat (person-eigene Doks) ----
        double rohP1 = 0;
        foreach (var d in e.Doks)
        {
            rohP1 += k.AusgangGewicht(d.Ausgang) * Decay(d.Zeitpunkt, jetztUtc, k.HalbwertszeitTage);
        }
        double p1 = Saettige(rohP1, k.CapP1, k.P1Nenner);

        // ---- P2: Bewaffnung & Eskalation (Sub-Caps PersonCapWaffen + FluechtigPunkte = CapP2) ----
        var effektiv = LebensstatusLogic.Effektiv(e.Lebensstatus, e.TotBis, jetztUtc);
        double waffenPkt = Saettige(e.DistinctWaffenCount, k.PersonCapWaffen, k.PersonWaffenNenner);
        double fluechtigPkt = effektiv == Lebensstatus.Fluechtig ? k.FluechtigPunkte : 0;
        double p2 = Math.Min(k.CapP2, waffenPkt + fluechtigPkt);

        // ---- P3: Observations-Heat (laufend wiegt mehr, beide zeit-abklingend) ----
        double rohP3 = 0;
        foreach (var o in e.Observationen)
        {
            double gewicht = o.Ende is null ? 1.0 : k.ObservationAbgeschlossenGewicht;
            rohP3 += gewicht * Decay(o.Beginn, jetztUtc, k.HalbwertszeitTage);
        }
        double p3 = Saettige(rohP3, k.CapP3, k.P3Nenner);

        // ---- P4: Soziale Gefahr (typisierte Beziehungen + Leitungsrollen) ----
        double rohP4 = k.FeindGewicht * e.FeindCount + k.VerbuendeterGewicht * e.VerbuendeterCount
                     + k.GpGewicht * e.GeschaeftspartnerCount + k.LeitungGewicht * e.LeitungsrollenCount;
        double p4 = Saettige(rohP4, k.CapP4, k.P4Nenner);

        // ---- P5: Netzwerk-Zentralität (manuelle Standard-Verknüpfungen) ----
        double p5 = Saettige(e.StandardKantenGrad, k.CapP5, k.P5Nenner);

        double inhalt = p1 + p2 + p3 + p4 + p5; // Caps 40+22+18+12+8 = 100 (Default)
        int sockel = BedrohungsScoreKonstanten.Sockel(e.Einstufung);
        int score = BandScore(inhalt, sockel);

        // ---- Person-Konfidenz (eigene Buckets, Summe = 1; senkt den Score NIE) ----
        double konf = 0.30 * Bit(e.Doks.Count > 0 || e.Observationen.Count > 0)
                    + 0.15 * Bit(e.DistinctWaffenCount > 0)
                    + 0.10 * Bit(e.Einstufung != Einstufung.Unbekannt)
                    + 0.10 * Bit(e.FeindCount + e.VerbuendeterCount + e.GeschaeftspartnerCount > 0 || e.StandardKantenGrad > 0)
                    + 0.10 * Bit(e.MitgliedschaftenCount > 0)
                    + 0.10 * Bit(e.Datenreichtum > 0)
                    + 0.15 * Bit(IstFrisch(e.JuengsteErfassungUtc, jetztUtc, k.KonfidenzFrischeTage));
        int konfidenz = Prozent(konf);

        bool triage = inhalt >= k.TriageSchwelle && e.Einstufung == Einstufung.Unbekannt;
        string? triageHinweis = TriageHinweis(triage, score, konfidenz);

        var teilscores = new List<BedrohungsTeilscore>
        {
            new("Maßnahmen-Heat", R1(rohP1), R1(p1), k.CapP1, P1TreiberPerson(e, jetztUtc)),
            new("Bewaffnung & Eskalation", R1(waffenPkt + fluechtigPkt), R1(p2), k.CapP2, P2TreiberPerson(e, effektiv)),
            new("Observations-Heat", R1(rohP3), R1(p3), k.CapP3, P3TreiberPerson(e)),
            new("Soziale Gefahr", R1(rohP4), R1(p4), k.CapP4, P4TreiberPerson(e)),
            new("Netzwerk-Zentralität", R1(e.StandardKantenGrad), R1(p5), k.CapP5, P5TreiberPerson(e)),
        };

        // Hinweis: Tot-Respawn-Countdown bewusst NICHT ins JSON (sonst eingefroren) – die UI rendert ihn on-read.
        return new BedrohungsScoreErgebnis(score, konfidenz,
            BaueDetail(teilscores, inhalt, e.Einstufung, sockel, score, konfidenz, triage, triageHinweis, jetztUtc));
    }

    // ---- Gemeinsame reine Helfer ----

    private static double Saettige(double roh, double cap, double nenner)
        => nenner <= 0 ? 0 : cap * (1 - Math.Exp(-roh / nenner));

    private static int BandScore(double inhalt, int sockel)
    {
        double scoreRaw = sockel + (100 - sockel) * inhalt / 100.0;
        return (int)Math.Clamp(Math.Round(scoreRaw, MidpointRounding.AwayFromZero), 0, 100);
    }

    private static double Decay(DateTime zeitpunkt, DateTime jetztUtc, double halbwertszeitTage)
    {
        if (halbwertszeitTage <= 0)
        {
            return 1.0;
        }
        var alterTage = Math.Max(0, (jetztUtc - zeitpunkt).TotalDays); // Zukunfts-Zeitpunkte → Alter 0
        return Math.Pow(0.5, alterTage / halbwertszeitTage);
    }

    private static bool IstFrisch(DateTime? juengste, DateTime jetztUtc, int frischeTage)
        => juengste is { } j && j <= jetztUtc && (jetztUtc - j).TotalDays < frischeTage;

    private static double Bit(bool b) => b ? 1 : 0;
    private static int Prozent(double anteil) => (int)Math.Clamp(Math.Round(anteil * 100, MidpointRounding.AwayFromZero), 0, 100);
    private static double R1(double x) => Math.Round(x, 1);

    private static string? TriageHinweis(bool triage, int score, int konfidenz) => triage
        ? "Hohe Aktivität, aber Einstufung „Unbekannt“ – bitte triagieren/einstufen."
        : (score >= 25 && konfidenz < 50 ? "Bewertet, aber dünn belegt – Daten nacherfassen." : null);

    private static BedrohungsScoreDetail BaueDetail(List<BedrohungsTeilscore> teilscores, double inhalt, Einstufung einstufung,
        int sockel, int score, int konfidenz, bool triage, string? triageHinweis, DateTime jetztUtc)
    {
        string bandHinweis = sockel == 0
            ? $"Einstufung {EinstufungName(einstufung)}: kein Mindest-Band – der Inhalt ({inhalt:0}) bestimmt den Score direkt."
            : $"Einstufung {EinstufungName(einstufung)} hebt in das Band ≥{sockel}: Inhalt {inhalt:0} ⇒ Score {score}.";
        return new BedrohungsScoreDetail
        {
            Teilscores = teilscores,
            Inhalt = R1(inhalt),
            EinstufungName = EinstufungName(einstufung),
            Sockel = sockel,
            BandHinweis = bandHinweis,
            Score = score,
            Konfidenz = konfidenz,
            TriageFlag = triage,
            TriageHinweis = triageHinweis,
            BerechnetAmUtc = jetztUtc,
        };
    }

    private static string EinstufungName(Einstufung e) => e switch
    {
        Einstufung.Prueffall => "Prüffall",
        Einstufung.Verdachtsfall => "Verdachtsfall",
        Einstufung.GesichertStaatsgefaehrdend => "Gesichert staatsgefährdend",
        _ => "Unbekannt",
    };

    private static IReadOnlyList<string> S1Treiber(BedrohungsScoreEingabe e, int dokAnzahl, DateTime jetztUtc)
    {
        var t = new List<string>();
        if (e.Aktivitaeten.Count > 0)
        {
            var tage = (int)Math.Max(0, (jetztUtc - e.Aktivitaeten.Max(a => a.Zeitpunkt)).TotalDays);
            t.Add($"{e.Aktivitaeten.Count} Aktivität(en), jüngste vor {tage} Tagen");
        }
        if (dokAnzahl > 0)
        {
            t.Add($"{dokAnzahl} Maßnahme(n) von Mitgliedern (im Mitgliedschaftszeitraum)");
        }
        if (t.Count == 0)
        {
            t.Add("keine datierten Vorfälle erfasst");
        }
        return t;
    }

    private static IReadOnlyList<string> S2Treiber(BedrohungsScoreEingabe e, double groesse)
    {
        var t = new List<string> { $"~{groesse:0} Mitglieder" };
        if (e.RaengeCount > 0) { t.Add($"{e.RaengeCount} Ränge"); }
        if (e.HatAktiveLeitung) { t.Add("Leitung erfasst"); }
        if (e.HatAnwesen) { t.Add("Anwesen erfasst"); }
        if (e.DistinctWaffenCount > 0) { t.Add($"{e.DistinctWaffenCount} Waffenarten"); }
        if (e.DrogenroutenCount > 0 || e.LagerbestandCount > 0) { t.Add($"{e.DrogenroutenCount} Routen / {e.LagerbestandCount} Lager"); }
        return t;
    }

    private static IReadOnlyList<string> S3Treiber(BedrohungsScoreEingabe e)
    {
        var t = new List<string>();
        if (e.KonfliktCount > 0) { t.Add($"{e.KonfliktCount} aktive(r) Konflikt(e)"); }
        if (e.BuendnisCount > 0) { t.Add($"{e.BuendnisCount} Bündnis(se)"); }
        if (t.Count == 0) { t.Add("keine Konflikte/Bündnisse"); }
        return t;
    }

    private static IReadOnlyList<string> S4Treiber(BedrohungsScoreEingabe e)
        => e.StandardKantenGrad > 0
            ? new[] { $"{e.StandardKantenGrad} sonstige Verknüpfung(en) im Netzwerk" }
            : new[] { "nicht vernetzt (keine sonstigen Verknüpfungen)" };

    private static IReadOnlyList<string> P1TreiberPerson(PersonBedrohungsScoreEingabe e, DateTime jetztUtc)
    {
        if (e.Doks.Count == 0)
        {
            return new[] { "keine Maßnahmen erfasst" };
        }
        var tage = (int)Math.Max(0, (jetztUtc - e.Doks.Max(d => d.Zeitpunkt)).TotalDays);
        return new[] { $"{e.Doks.Count} Maßnahme(n), jüngste vor {tage} Tagen" };
    }

    private static IReadOnlyList<string> P2TreiberPerson(PersonBedrohungsScoreEingabe e, Lebensstatus effektiv)
    {
        var t = new List<string>();
        if (e.DistinctWaffenCount > 0) { t.Add($"{e.DistinctWaffenCount} Waffe(n)"); }
        if (effektiv == Lebensstatus.Fluechtig) { t.Add("flüchtig (gesucht)"); }
        if (t.Count == 0) { t.Add("unbewaffnet, nicht flüchtig"); }
        return t;
    }

    private static IReadOnlyList<string> P3TreiberPerson(PersonBedrohungsScoreEingabe e)
    {
        if (e.Observationen.Count == 0)
        {
            return new[] { "keine Observationen" };
        }
        var laufend = e.Observationen.Count(o => o.Ende is null);
        return new[] { $"{e.Observationen.Count} Observation(en){(laufend > 0 ? $", {laufend} laufend" : "")}" };
    }

    private static IReadOnlyList<string> P4TreiberPerson(PersonBedrohungsScoreEingabe e)
    {
        var t = new List<string>();
        if (e.FeindCount > 0) { t.Add($"{e.FeindCount} Feind(e)"); }
        if (e.VerbuendeterCount > 0) { t.Add($"{e.VerbuendeterCount} Verbündete(r)"); }
        if (e.GeschaeftspartnerCount > 0) { t.Add($"{e.GeschaeftspartnerCount} Geschäftspartner"); }
        if (e.LeitungsrollenCount > 0) { t.Add($"{e.LeitungsrollenCount} Leitungsrolle(n)"); }
        if (t.Count == 0) { t.Add("keine relevanten Beziehungen/Leitung"); }
        return t;
    }

    private static IReadOnlyList<string> P5TreiberPerson(PersonBedrohungsScoreEingabe e)
        => e.StandardKantenGrad > 0
            ? new[] { $"{e.StandardKantenGrad} sonstige Verknüpfung(en) im Netzwerk" }
            : new[] { "nicht vernetzt (keine sonstigen Verknüpfungen)" };

    // =====================================================================================
    //  DB-Anbindung: Rohdaten flach laden (WHERE FK IN), berechnen, via ExecuteUpdate persistieren.
    //  Die Konfiguration wird je öffentlichem Aufruf EINMAL geladen (gecacht) und durchgereicht.
    // =====================================================================================

    public async Task NeuBerechnenAsync(string fraktionId, CancellationToken cancellationToken = default)
    {
        var konfig = await konfigService.GetAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await BerechneFraktionAsync(db, fraktionId, konfig, cancellationToken);
    }

    public async Task NeuBerechnenFuerPersonAsync(string personId, CancellationToken cancellationToken = default)
    {
        var konfig = await konfigService.GetAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Alle je-Mitgliedschaften (inkl. ausgetretener) → austritts-stabil, da eine alte Tat weiterzählt.
        var fraktionIds = await db.FraktionMitglieder.IgnoreQueryFilters()
            .Where(m => m.PersonId == personId)
            .Select(m => m.FraktionId)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var id in fraktionIds)
        {
            await BerechneFraktionAsync(db, id, konfig, cancellationToken);
        }
    }

    public async Task<int> NeuBerechnenAlleAsync(CancellationToken cancellationToken = default)
    {
        var konfig = await konfigService.GetAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ids = await db.Fraktionen.Select(f => f.Id).ToListAsync(cancellationToken); // Soft-Delete-Filter aktiv
        foreach (var id in ids)
        {
            await BerechneFraktionAsync(db, id, konfig, cancellationToken);
        }
        return ids.Count;
    }

    public async Task NeuBerechnenPersonScoreAsync(string personId, CancellationToken cancellationToken = default)
    {
        var konfig = await konfigService.GetAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await BerechnePersonAsync(db, personId, konfig, cancellationToken);
    }

    public async Task<int> NeuBerechnenAllePersonenScoresAsync(CancellationToken cancellationToken = default)
    {
        var konfig = await konfigService.GetAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ids = await db.Personen.Select(p => p.Id).ToListAsync(cancellationToken); // Soft-Delete-Filter aktiv
        foreach (var id in ids)
        {
            await BerechnePersonAsync(db, id, konfig, cancellationToken);
        }
        return ids.Count;
    }

    private static async Task BerechneFraktionAsync(AppDbContext db, string fraktionId, BedrohungsScoreKonfiguration konfig, CancellationToken ct)
    {
        // Scalar-Felder der Fraktion (Soft-Delete-Filter blendet gelöschte aus → kein Treffer → kein Recompute).
        var f = await db.Fraktionen
            .Where(x => x.Id == fraktionId)
            .Select(x => new
            {
                x.IstStaatsfraktion,
                x.Einstufung,
                x.GeschaetzteMitgliederzahl,
                x.Anwesen,
                Erfasst = x.GeaendertAm ?? x.ErstelltAm,
            })
            .FirstOrDefaultAsync(ct);
        if (f is null)
        {
            return;
        }

        BedrohungsScoreEingabe eingabe = f.IstStaatsfraktion
            ? new BedrohungsScoreEingabe { IstStaatsfraktion = true, Einstufung = f.Einstufung }
            : await LadeEingabeAsync(db, fraktionId, f.Einstufung, f.GeschaetzteMitgliederzahl,
                !string.IsNullOrWhiteSpace(f.Anwesen), f.Erfasst, ct);

        var ergebnis = Berechne(eingabe, DateTime.UtcNow, konfig);
        await PersistiereFraktionAsync(db, fraktionId, ergebnis, ct);
    }

    private static async Task BerechnePersonAsync(AppDbContext db, string personId, BedrohungsScoreKonfiguration konfig, CancellationToken ct)
    {
        var p = await db.Personen
            .Where(x => x.Id == personId)
            .Select(x => new
            {
                x.Einstufung,
                x.Lebensstatus,
                x.TotBis,
                Erfasst = x.GeaendertAm ?? x.ErstelltAm,
            })
            .FirstOrDefaultAsync(ct);
        if (p is null)
        {
            return;
        }

        var eingabe = await LadePersonEingabeAsync(db, personId, p.Einstufung, p.Lebensstatus, p.TotBis, p.Erfasst, ct);
        var ergebnis = BerechnePerson(eingabe, DateTime.UtcNow, konfig);
        await PersistierePersonAsync(db, personId, ergebnis, ct);
    }

    private static async Task<BedrohungsScoreEingabe> LadeEingabeAsync(AppDbContext db, string fraktionId,
        Einstufung einstufung, int? geschaetzt, bool hatAnwesen, DateTime fraktionErfasstUtc, CancellationToken ct)
    {
        // Aktive Mitglieder (= nicht ausgetreten; globaler Soft-Delete-Filter greift).
        var aktiveMitglieder = await db.FraktionMitglieder
            .Where(m => m.FraktionId == fraktionId)
            .Select(m => new { m.IstLeitung, m.ErstelltAm, m.GeaendertAm })
            .ToListAsync(ct);

        int raenge = await db.FraktionRaenge.Where(r => r.FraktionId == fraktionId).CountAsync(ct);

        var waffen = await db.FraktionWaffenbestaende.Where(w => w.FraktionId == fraktionId).Select(w => w.Bezeichnung).ToListAsync(ct);
        var lager = await db.FraktionLagerbestaende.Where(l => l.FraktionId == fraktionId).Select(l => l.Bezeichnung).ToListAsync(ct);
        var routen = await db.FraktionDrogenrouten.Where(d => d.FraktionId == fraktionId).Select(d => d.Bezeichnung).ToListAsync(ct);

        var aktivitaetenRows = await db.FraktionAktivitaeten
            .Where(a => a.FraktionId == fraktionId)
            .Select(a => new { a.Art, a.Zeitpunkt, a.ErstelltAm, a.GeaendertAm })
            .ToListAsync(ct);
        var aktivitaeten = aktivitaetenRows.Select(a => new BedrohungsAktivitaet(a.Art, a.Zeitpunkt)).ToList();

        // Mitgliedschafts-Perioden inkl. ausgetretener (IgnoreQueryFilters) → austritts-stabiler Heat.
        var perioden = await db.FraktionMitglieder.IgnoreQueryFilters()
            .Where(m => m.FraktionId == fraktionId && m.PersonId != "")
            .Select(m => new { m.PersonId, Beitritt = m.ErstelltAm, Austritt = m.GeloeschtAm })
            .ToListAsync(ct);
        var personIds = perioden.Select(p => p.PersonId).Distinct().ToList();

        var dokRows = personIds.Count == 0
            ? new List<DokRow>()
            : (await db.PersonDoks
                .Where(d => personIds.Contains(d.PersonId))
                .Select(d => new { d.PersonId, d.Ausgang, d.Zeitpunkt })
                .ToListAsync(ct))
              .Select(d => new DokRow(d.PersonId, d.Ausgang, d.Zeitpunkt))
              .ToList();
        var doksByPerson = dokRows.GroupBy(d => d.PersonId).ToDictionary(g => g.Key, g => g.ToList());

        var doksProMitglied = new List<IReadOnlyList<BedrohungsDok>>();
        foreach (var p in perioden)
        {
            if (!doksByPerson.TryGetValue(p.PersonId, out var personDoks))
            {
                continue;
            }
            var bis = p.Austritt ?? DateTime.MaxValue;
            var imFenster = personDoks
                .Where(d => d.Zeitpunkt >= p.Beitritt && d.Zeitpunkt <= bis)
                .Select(d => new BedrohungsDok(d.Ausgang, d.Zeitpunkt))
                .ToList();
            if (imFenster.Count > 0)
            {
                doksProMitglied.Add(imFenster);
            }
        }

        var arten = await db.Verknuepfungen
            .Where(v => !v.Automatisch
                && (v.Art == VerknuepfungArt.Konflikt || v.Art == VerknuepfungArt.Buendnis)
                && ((v.VonTyp == nameof(Fraktion) && v.VonId == fraktionId)
                 || (v.NachTyp == nameof(Fraktion) && v.NachId == fraktionId)))
            .Select(v => v.Art)
            .ToListAsync(ct);

        var standardKantenGrad = await db.Verknuepfungen
            .Where(v => !v.Automatisch
                && v.Art == VerknuepfungArt.Standard
                && ((v.VonTyp == nameof(Fraktion) && v.VonId == fraktionId)
                 || (v.NachTyp == nameof(Fraktion) && v.NachId == fraktionId)))
            .CountAsync(ct);

        DateTime? juengste = fraktionErfasstUtc;
        foreach (var a in aktivitaetenRows)
        {
            juengste = Spaeter(juengste, a.GeaendertAm ?? a.ErstelltAm);
        }
        foreach (var m in aktiveMitglieder)
        {
            juengste = Spaeter(juengste, m.GeaendertAm ?? m.ErstelltAm);
        }
        var letzteEinstufung = await db.EinstufungVerlauf
            .Where(ev => ev.EntitaetTyp == nameof(Fraktion) && ev.EntitaetId == fraktionId)
            .OrderByDescending(ev => ev.Zeitpunkt)
            .Select(ev => (DateTime?)ev.Zeitpunkt)
            .FirstOrDefaultAsync(ct);
        juengste = Spaeter(juengste, letzteEinstufung);

        return new BedrohungsScoreEingabe
        {
            IstStaatsfraktion = false,
            Einstufung = einstufung,
            GeschaetzteMitgliederzahl = geschaetzt,
            AktiveMitgliederCount = aktiveMitglieder.Count,
            HatAktiveLeitung = aktiveMitglieder.Any(m => m.IstLeitung),
            RaengeCount = raenge,
            HatAnwesen = hatAnwesen,
            DistinctWaffenCount = DistinctNichtLeer(waffen),
            LagerbestandCount = DistinctNichtLeer(lager),
            DrogenroutenCount = DistinctNichtLeer(routen),
            Aktivitaeten = aktivitaeten,
            DoksProMitglied = doksProMitglied,
            KonfliktCount = arten.Count(a => a == VerknuepfungArt.Konflikt),
            BuendnisCount = arten.Count(a => a == VerknuepfungArt.Buendnis),
            StandardKantenGrad = standardKantenGrad,
            JuengsteErfassungUtc = juengste,
        };
    }

    private static async Task<PersonBedrohungsScoreEingabe> LadePersonEingabeAsync(AppDbContext db, string personId,
        Einstufung einstufung, Lebensstatus lebensstatus, DateTime? totBis, DateTime personErfasstUtc, CancellationToken ct)
    {
        var dokRows = await db.PersonDoks
            .Where(d => d.PersonId == personId)
            .Select(d => new { d.Ausgang, d.Zeitpunkt, d.ErstelltAm, d.GeaendertAm })
            .ToListAsync(ct);
        var doks = dokRows.Select(d => new BedrohungsDok(d.Ausgang, d.Zeitpunkt)).ToList();

        var waffen = await db.PersonWaffen.Where(w => w.PersonId == personId).Select(w => w.Text).ToListAsync(ct);

        var obsRows = await db.Observationen
            .Where(o => o.PersonId == personId)
            .Select(o => new { o.Beginn, o.Ende, o.ErstelltAm, o.GeaendertAm })
            .ToListAsync(ct);
        var observationen = obsRows.Select(o => new BedrohungsObservation(o.Beginn, o.Ende)).ToList();

        var beziehungsTypen = await db.PersonBeziehungen
            .Where(b => b.PersonAId == personId || b.PersonBId == personId)
            .Select(b => b.Typ)
            .ToListAsync(ct);

        // Leitungsrollen + Mitgliedschaften über alle drei Mitglied-Tabellen (aktiv = Soft-Delete-Filter).
        int leitungFr = await db.FraktionMitglieder.Where(m => m.PersonId == personId && m.IstLeitung).CountAsync(ct);
        int leitungGr = await db.PersonengruppeMitglieder.Where(m => m.PersonId == personId && m.IstLeitung).CountAsync(ct);
        int leitungPa = await db.ParteiMitglieder.Where(m => m.PersonId == personId && m.IstLeitung).CountAsync(ct);
        int mitglFr = await db.FraktionMitglieder.Where(m => m.PersonId == personId).CountAsync(ct);
        int mitglGr = await db.PersonengruppeMitglieder.Where(m => m.PersonId == personId).CountAsync(ct);
        int mitglPa = await db.ParteiMitglieder.Where(m => m.PersonId == personId).CountAsync(ct);

        var standardKantenGrad = await db.Verknuepfungen
            .Where(v => !v.Automatisch
                && v.Art == VerknuepfungArt.Standard
                && ((v.VonTyp == nameof(Person) && v.VonId == personId)
                 || (v.NachTyp == nameof(Person) && v.NachId == personId)))
            .CountAsync(ct);

        int aliase = await db.PersonAliase.Where(a => a.PersonId == personId).CountAsync(ct);
        int fahrzeuge = await db.PersonFahrzeuge.Where(f => f.PersonId == personId).CountAsync(ct);
        int telefone = await db.PersonTelefone.Where(t => t.PersonId == personId).CountAsync(ct);
        int orte = await db.PersonOrte.Where(o => o.PersonId == personId).CountAsync(ct);

        // Jüngste *Erfassung* (Erfassungszeit, nicht RP-Zeit): Person + jüngstes Dok/Observation.
        DateTime? juengste = personErfasstUtc;
        foreach (var d in dokRows)
        {
            juengste = Spaeter(juengste, d.GeaendertAm ?? d.ErstelltAm);
        }
        foreach (var o in obsRows)
        {
            juengste = Spaeter(juengste, o.GeaendertAm ?? o.ErstelltAm);
        }

        return new PersonBedrohungsScoreEingabe
        {
            Einstufung = einstufung,
            Lebensstatus = lebensstatus,
            TotBis = totBis,
            Doks = doks,
            DistinctWaffenCount = DistinctNichtLeer(waffen),
            Observationen = observationen,
            FeindCount = beziehungsTypen.Count(t => t == BeziehungsTyp.Feind),
            VerbuendeterCount = beziehungsTypen.Count(t => t == BeziehungsTyp.Verbuendeter),
            GeschaeftspartnerCount = beziehungsTypen.Count(t => t == BeziehungsTyp.Geschaeftspartner),
            LeitungsrollenCount = leitungFr + leitungGr + leitungPa,
            StandardKantenGrad = standardKantenGrad,
            MitgliedschaftenCount = mitglFr + mitglGr + mitglPa,
            Datenreichtum = aliase + fahrzeuge + telefone + orte,
            JuengsteErfassungUtc = juengste,
        };
    }

    private static async Task PersistiereFraktionAsync(AppDbContext db, string fraktionId, BedrohungsScoreErgebnis erg, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(erg.Detail, JsonOptions);
        // Bewusst per ExecuteUpdate am Audit-Interceptor vorbei: kein GeaendertAm-Stempel (sonst verfälschte
        // Aktualitäts-Ampel) und keine AuditLog-Zeile je Recompute. Der Soft-Delete-Filter greift weiterhin.
        await db.Fraktionen
            .Where(f => f.Id == fraktionId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.BedrohungsScore, erg.Score)
                .SetProperty(f => f.BedrohungsKonfidenz, erg.Konfidenz)
                .SetProperty(f => f.BedrohungsDetailJson, json)
                .SetProperty(f => f.ScoreBerechnetAm, erg.Detail.BerechnetAmUtc), ct);
    }

    private static async Task PersistierePersonAsync(AppDbContext db, string personId, BedrohungsScoreErgebnis erg, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(erg.Detail, JsonOptions);
        await db.Personen
            .Where(p => p.Id == personId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.BedrohungsScore, erg.Score)
                .SetProperty(p => p.BedrohungsKonfidenz, erg.Konfidenz)
                .SetProperty(p => p.BedrohungsDetailJson, json)
                .SetProperty(p => p.ScoreBerechnetAm, erg.Detail.BerechnetAmUtc), ct);
    }

    private static int DistinctNichtLeer(IEnumerable<string> werte) => werte
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Select(s => s.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    private static DateTime? Spaeter(DateTime? a, DateTime? b) => (a, b) switch
    {
        (null, _) => b,
        (_, null) => a,
        _ => a.Value >= b.Value ? a : b,
    };

    private readonly record struct DokRow(string PersonId, MassnahmeAusgang Ausgang, DateTime Zeitpunkt);
}
