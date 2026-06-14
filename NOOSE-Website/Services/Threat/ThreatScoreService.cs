using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Threat;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IBedrohungsScoreService" />
public class ThreatScoreService(IDbContextFactory<AppDbContext> dbFactory, IThreatScoreConfigService configService)
    : IThreatScoreService
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
    public static ThreatScoreResult Calculate(ThreatScoreInput e, DateTime nowUtc, ThreatScoreConfiguration k)
    {
        if (e.IsStateFaction)
        {
            return new ThreatScoreResult(null, null, new ThreatScoreDetail
            {
                Excluded = "Staatsfraktion",
                ClassificationName = ClassificationName(e.Classification),
                BandHint = "Staatsfraktion – vom Bedrohungs-Score ausgenommen.",
                CalculatedAtUtc = nowUtc,
            });
        }

        // ---- S1: Aktivitäts- & Maßnahmen-Heat ----
        double aktHeat = 0;
        foreach (var a in e.Activities)
        {
            aktHeat += k.KindWeight(a.Kind) * Decay(a.Timestamp, nowUtc, k.HalfLifeDays);
        }
        double docHeat = 0;
        int docCount = 0;
        foreach (var docs in e.DocsPerMember)
        {
            double perMember = 0;
            foreach (var d in docs)
            {
                perMember += k.OutcomeWeight(d.Outcome) * Decay(d.Timestamp, nowUtc, k.HalfLifeDays);
                docCount++;
            }
            docHeat += Math.Min(k.PerMemberDocCap, perMember);
        }
        double rawS1 = aktHeat + k.DocHeatWeight * docHeat;
        double s1 = Saturate(rawS1, k.CapS1, k.S1Denominator);

        // ---- S2: Organisation & Reichweite (Sub-Caps summieren auf CapS2) ----
        double size = Math.Max(e.EstimatedMemberCount ?? 0, e.ActiveMembersCount);
        double sizePkt = Saturate(size, k.CapSize, k.SizeDenominator);
        double structurePkt = Math.Min(k.RanksMaxPoints, e.RanksCount)
                           + (e.HasActiveLead ? k.LeadPoints : 0)
                           + (e.HasEstate ? k.EstatePoints : 0);
        double weaponsPkt = Saturate(e.DistinctWeaponsCount, k.CapWeapons, k.WeaponsDenominator);
        double infraRaw = k.DrugRouteWeight * e.DrugRoutesCount + e.InventoryCount;
        double infraPkt = Saturate(infraRaw, k.CapInfra, k.InfraDenominator);
        double s2 = Math.Min(k.CapS2, sizePkt + structurePkt + weaponsPkt + infraPkt);

        // ---- S3: Konflikt & Bündnis ----
        double rawS3 = k.ConflictWeight * e.ConflictCount + k.AllianceWeight * e.AllianceCount;
        double s3 = Saturate(rawS3, k.CapS3, k.S3Denominator);

        // ---- S4: Netzwerk-Zentralität (manuelle Standard-Verknüpfungen; disjunkt zu S3, orthogonal zu S2) ----
        double rawS4 = e.DefaultEdgesDegree;
        double s4 = Saturate(rawS4, k.CapS4, k.S4Denominator);

        double content = s1 + s2 + s3 + s4; // Caps 55+22+15+8 = 100 (Default)
        int @base = ThreatScoreConstants.Base(e.Classification);
        int score = BandScore(content, @base);

        // ---- Daten-Konfidenz (separat, senkt den Score NIE) ----
        bool hasDocs = docCount > 0;
        bool hasStocks = (e.DistinctWeaponsCount + e.InventoryCount + e.DrugRoutesCount) > 0;
        double konf = 0.30 * Bit(e.Activities.Count > 0 || hasDocs)
                    + 0.20 * Bit(e.ActiveMembersCount > 0)
                    + 0.15 * Bit(hasStocks)
                    + 0.10 * Bit(e.EstimatedMemberCount.HasValue)
                    + 0.10 * Bit(e.Classification != Classification.Unknown)
                    + 0.15 * Bit(IsFresh(e.LatestCaptureUtc, nowUtc, k.ConfidenceFreshDays));
        int confidence = Percent(konf);

        bool triage = content >= k.TriageThreshold && e.Classification == Classification.Unknown;
        string? triageHint = TriageHint(triage, score, confidence);

        var partialScores = new List<ThreatPartialScore>
        {
            new("Aktivitäts- & Maßnahmen-Heat", R1(rawS1), R1(s1), k.CapS1, S1Driver(e, docCount, nowUtc)),
            new("Organisation & Reichweite", R1(sizePkt + structurePkt + weaponsPkt + infraPkt), R1(s2), k.CapS2, S2Driver(e, size)),
            new("Konflikt & Bündnis", R1(rawS3), R1(s3), k.CapS3, S3Driver(e)),
            new("Netzwerk-Zentralität", R1(rawS4), R1(s4), k.CapS4, S4Driver(e)),
        };

        return new ThreatScoreResult(score, confidence,
            BuildDetail(partialScores, content, e.Classification, @base, score, confidence, triage, triageHint, nowUtc));
    }

    // =====================================================================================
    //  Reine PERSON-Berechnung (P1–P5, Band-Projektion, separate Konfidenz). Nur person-eigene
    //  Daten → keine Zirkularität mit dem Fraktion-Score.
    // =====================================================================================

    /// <summary>Berechnet Score/Konfidenz/Aufschlüsselung einer Person. Lebensstatus on-read via <c>LebensstatusLogic.Effektiv</c>.</summary>
    public static ThreatScoreResult CalculatePerson(PersonThreatScoreInput e, DateTime nowUtc, ThreatScoreConfiguration k)
    {
        // ---- P1: Maßnahmen-Heat (person-eigene Doks) ----
        double rawP1 = 0;
        foreach (var d in e.Docs)
        {
            rawP1 += k.OutcomeWeight(d.Outcome) * Decay(d.Timestamp, nowUtc, k.HalfLifeDays);
        }
        double p1 = Saturate(rawP1, k.CapP1, k.P1Denominator);

        // ---- P2: Bewaffnung & Eskalation (Sub-Caps PersonCapWaffen + FluechtigPunkte = CapP2) ----
        var effective = LifeStatusLogic.Effective(e.LifeStatus, e.DeadUntil, nowUtc);
        double weaponsPkt = Saturate(e.DistinctWeaponsCount, k.PersonCapWeapons, k.PersonWeaponsDenominator);
        double fugitivePkt = effective == LifeStatus.Fugitive ? k.FugitivePoints : 0;
        double p2 = Math.Min(k.CapP2, weaponsPkt + fugitivePkt);

        // ---- P3: Observations-Heat (laufend wiegt mehr, beide zeit-abklingend) ----
        double rawP3 = 0;
        foreach (var o in e.Observations)
        {
            double weight = o.End is null ? 1.0 : k.ObservationCompletedWeight;
            rawP3 += weight * Decay(o.Start, nowUtc, k.HalfLifeDays);
        }
        double p3 = Saturate(rawP3, k.CapP3, k.P3Denominator);

        // ---- P4: Soziale Gefahr (typisierte Beziehungen + Leitungsrollen) ----
        double rawP4 = k.EnemyWeight * e.EnemyCount + k.AllyWeight * e.AllyCount
                     + k.GpWeight * e.BusinessPartnerCount + k.LeadWeight * e.LeadershipRolesCount;
        double p4 = Saturate(rawP4, k.CapP4, k.P4Denominator);

        // ---- P5: Netzwerk-Zentralität (manuelle Standard-Verknüpfungen) ----
        double p5 = Saturate(e.DefaultEdgesDegree, k.CapP5, k.P5Denominator);

        double content = p1 + p2 + p3 + p4 + p5; // Caps 40+22+18+12+8 = 100 (Default)
        int @base = ThreatScoreConstants.Base(e.Classification);
        int score = BandScore(content, @base);

        // ---- Person-Konfidenz (eigene Buckets, Summe = 1; senkt den Score NIE) ----
        double konf = 0.30 * Bit(e.Docs.Count > 0 || e.Observations.Count > 0)
                    + 0.15 * Bit(e.DistinctWeaponsCount > 0)
                    + 0.10 * Bit(e.Classification != Classification.Unknown)
                    + 0.10 * Bit(e.EnemyCount + e.AllyCount + e.BusinessPartnerCount > 0 || e.DefaultEdgesDegree > 0)
                    + 0.10 * Bit(e.MembershipsCount > 0)
                    + 0.10 * Bit(e.DataRichness > 0)
                    + 0.15 * Bit(IsFresh(e.LatestCaptureUtc, nowUtc, k.ConfidenceFreshDays));
        int confidence = Percent(konf);

        bool triage = content >= k.TriageThreshold && e.Classification == Classification.Unknown;
        string? triageHint = TriageHint(triage, score, confidence);

        var partialScores = new List<ThreatPartialScore>
        {
            new("Maßnahmen-Heat", R1(rawP1), R1(p1), k.CapP1, P1DriverPerson(e, nowUtc)),
            new("Bewaffnung & Eskalation", R1(weaponsPkt + fugitivePkt), R1(p2), k.CapP2, P2DriverPerson(e, effective)),
            new("Observations-Heat", R1(rawP3), R1(p3), k.CapP3, P3DriverPerson(e)),
            new("Soziale Gefahr", R1(rawP4), R1(p4), k.CapP4, P4DriverPerson(e)),
            new("Netzwerk-Zentralität", R1(e.DefaultEdgesDegree), R1(p5), k.CapP5, P5DriverPerson(e)),
        };

        // Hinweis: Tot-Respawn-Countdown bewusst NICHT ins JSON (sonst eingefroren) – die UI rendert ihn on-read.
        return new ThreatScoreResult(score, confidence,
            BuildDetail(partialScores, content, e.Classification, @base, score, confidence, triage, triageHint, nowUtc));
    }

    // ---- Gemeinsame reine Helfer ----

    private static double Saturate(double raw, double cap, double denominator)
        => denominator <= 0 ? 0 : cap * (1 - Math.Exp(-raw / denominator));

    private static int BandScore(double content, int @base)
    {
        double scoreRaw = @base + (100 - @base) * content / 100.0;
        return (int)Math.Clamp(Math.Round(scoreRaw, MidpointRounding.AwayFromZero), 0, 100);
    }

    private static double Decay(DateTime timestamp, DateTime nowUtc, double halfLifeDays)
    {
        if (halfLifeDays <= 0)
        {
            return 1.0;
        }
        var alterDays = Math.Max(0, (nowUtc - timestamp).TotalDays); // Zukunfts-Zeitpunkte → Alter 0
        return Math.Pow(0.5, alterDays / halfLifeDays);
    }

    private static bool IsFresh(DateTime? latest, DateTime nowUtc, int freshDays)
        => latest is { } j && j <= nowUtc && (nowUtc - j).TotalDays < freshDays;

    private static double Bit(bool b) => b ? 1 : 0;
    private static int Percent(double share) => (int)Math.Clamp(Math.Round(share * 100, MidpointRounding.AwayFromZero), 0, 100);
    private static double R1(double x) => Math.Round(x, 1);

    private static string? TriageHint(bool triage, int score, int confidence) => triage
        ? "Hohe Aktivität, aber Einstufung „Unbekannt“ – bitte triagieren/einstufen."
        : (score >= 25 && confidence < 50 ? "Bewertet, aber dünn belegt – Daten nacherfassen." : null);

    private static ThreatScoreDetail BuildDetail(List<ThreatPartialScore> partialScores, double content, Classification classification,
        int @base, int score, int confidence, bool triage, string? triageHint, DateTime nowUtc)
    {
        string bandHint = @base == 0
            ? $"Einstufung {ClassificationName(classification)}: kein Mindest-Band – der Inhalt ({content:0}) bestimmt den Score direkt."
            : $"Einstufung {ClassificationName(classification)} hebt in das Band ≥{@base}: Inhalt {content:0} ⇒ Score {score}.";
        return new ThreatScoreDetail
        {
            PartialScores = partialScores,
            Content = R1(content),
            ClassificationName = ClassificationName(classification),
            Base = @base,
            BandHint = bandHint,
            Score = score,
            Confidence = confidence,
            TriageFlag = triage,
            TriageHint = triageHint,
            CalculatedAtUtc = nowUtc,
        };
    }

    private static string ClassificationName(Classification e) => e switch
    {
        Classification.ReviewCase => "Prüffall",
        Classification.SuspicionCase => "Verdachtsfall",
        Classification.SecuredStateThreatening => "Gesichert staatsgefährdend",
        _ => "Unbekannt",
    };

    private static IReadOnlyList<string> S1Driver(ThreatScoreInput e, int docCount, DateTime nowUtc)
    {
        var t = new List<string>();
        if (e.Activities.Count > 0)
        {
            var days = (int)Math.Max(0, (nowUtc - e.Activities.Max(a => a.Timestamp)).TotalDays);
            t.Add($"{e.Activities.Count} Aktivität(en), jüngste vor {days} Tagen");
        }
        if (docCount > 0)
        {
            t.Add($"{docCount} Maßnahme(n) von Mitgliedern (im Mitgliedschaftszeitraum)");
        }
        if (t.Count == 0)
        {
            t.Add("keine datierten Vorfälle erfasst");
        }
        return t;
    }

    private static IReadOnlyList<string> S2Driver(ThreatScoreInput e, double size)
    {
        var t = new List<string> { $"~{size:0} Mitglieder" };
        if (e.RanksCount > 0) { t.Add($"{e.RanksCount} Ränge"); }
        if (e.HasActiveLead) { t.Add("Leitung erfasst"); }
        if (e.HasEstate) { t.Add("Anwesen erfasst"); }
        if (e.DistinctWeaponsCount > 0) { t.Add($"{e.DistinctWeaponsCount} Waffenarten"); }
        if (e.DrugRoutesCount > 0 || e.InventoryCount > 0) { t.Add($"{e.DrugRoutesCount} Routen / {e.InventoryCount} Lager"); }
        return t;
    }

    private static IReadOnlyList<string> S3Driver(ThreatScoreInput e)
    {
        var t = new List<string>();
        if (e.ConflictCount > 0) { t.Add($"{e.ConflictCount} aktive(r) Konflikt(e)"); }
        if (e.AllianceCount > 0) { t.Add($"{e.AllianceCount} Bündnis(se)"); }
        if (t.Count == 0) { t.Add("keine Konflikte/Bündnisse"); }
        return t;
    }

    private static IReadOnlyList<string> S4Driver(ThreatScoreInput e)
        => e.DefaultEdgesDegree > 0
            ? new[] { $"{e.DefaultEdgesDegree} sonstige Verknüpfung(en) im Netzwerk" }
            : new[] { "nicht vernetzt (keine sonstigen Verknüpfungen)" };

    private static IReadOnlyList<string> P1DriverPerson(PersonThreatScoreInput e, DateTime nowUtc)
    {
        if (e.Docs.Count == 0)
        {
            return new[] { "keine Maßnahmen erfasst" };
        }
        var days = (int)Math.Max(0, (nowUtc - e.Docs.Max(d => d.Timestamp)).TotalDays);
        return new[] { $"{e.Docs.Count} Maßnahme(n), jüngste vor {days} Tagen" };
    }

    private static IReadOnlyList<string> P2DriverPerson(PersonThreatScoreInput e, LifeStatus effective)
    {
        var t = new List<string>();
        if (e.DistinctWeaponsCount > 0) { t.Add($"{e.DistinctWeaponsCount} Waffe(n)"); }
        if (effective == LifeStatus.Fugitive) { t.Add("flüchtig (gesucht)"); }
        if (t.Count == 0) { t.Add("unbewaffnet, nicht flüchtig"); }
        return t;
    }

    private static IReadOnlyList<string> P3DriverPerson(PersonThreatScoreInput e)
    {
        if (e.Observations.Count == 0)
        {
            return new[] { "keine Observationen" };
        }
        var running = e.Observations.Count(o => o.End is null);
        return new[] { $"{e.Observations.Count} Observation(en){(running > 0 ? $", {running} laufend" : "")}" };
    }

    private static IReadOnlyList<string> P4DriverPerson(PersonThreatScoreInput e)
    {
        var t = new List<string>();
        if (e.EnemyCount > 0) { t.Add($"{e.EnemyCount} Feind(e)"); }
        if (e.AllyCount > 0) { t.Add($"{e.AllyCount} Verbündete(r)"); }
        if (e.BusinessPartnerCount > 0) { t.Add($"{e.BusinessPartnerCount} Geschäftspartner"); }
        if (e.LeadershipRolesCount > 0) { t.Add($"{e.LeadershipRolesCount} Leitungsrolle(n)"); }
        if (t.Count == 0) { t.Add("keine relevanten Beziehungen/Leitung"); }
        return t;
    }

    private static IReadOnlyList<string> P5DriverPerson(PersonThreatScoreInput e)
        => e.DefaultEdgesDegree > 0
            ? new[] { $"{e.DefaultEdgesDegree} sonstige Verknüpfung(en) im Netzwerk" }
            : new[] { "nicht vernetzt (keine sonstigen Verknüpfungen)" };

    // =====================================================================================
    //  DB-Anbindung: Rohdaten flach laden (WHERE FK IN), berechnen, via ExecuteUpdate persistieren.
    //  Die Konfiguration wird je öffentlichem Aufruf EINMAL geladen (gecacht) und durchgereicht.
    // =====================================================================================

    public async Task NewCalculateAsync(string factionId, CancellationToken cancellationToken = default)
    {
        var config = await configService.GetAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await CalculateFactionAsync(db, factionId, config, cancellationToken);
    }

    public async Task NewCalculateForPersonAsync(string personId, CancellationToken cancellationToken = default)
    {
        var config = await configService.GetAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Alle je-Mitgliedschaften (inkl. ausgetretener) → austritts-stabil, da eine alte Tat weiterzählt.
        var factionIds = await db.FactionMembers.IgnoreQueryFilters()
            .Where(m => m.PersonId == personId)
            .Select(m => m.FactionId)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var id in factionIds)
        {
            await CalculateFactionAsync(db, id, config, cancellationToken);
        }
    }

    public async Task<int> NewCalculateAllAsync(CancellationToken cancellationToken = default)
    {
        var config = await configService.GetAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ids = await db.Factions.Select(f => f.Id).ToListAsync(cancellationToken); // Soft-Delete-Filter aktiv
        foreach (var id in ids)
        {
            await CalculateFactionAsync(db, id, config, cancellationToken);
        }
        return ids.Count;
    }

    public async Task NewCalculatePersonScoreAsync(string personId, CancellationToken cancellationToken = default)
    {
        var config = await configService.GetAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await CalculatePersonAsync(db, personId, config, cancellationToken);
    }

    public async Task<int> NewCalculateAllPeopleScoresAsync(CancellationToken cancellationToken = default)
    {
        var config = await configService.GetAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ids = await db.People.Select(p => p.Id).ToListAsync(cancellationToken); // Soft-Delete-Filter aktiv
        foreach (var id in ids)
        {
            await CalculatePersonAsync(db, id, config, cancellationToken);
        }
        return ids.Count;
    }

    private static async Task CalculateFactionAsync(AppDbContext db, string factionId, ThreatScoreConfiguration config, CancellationToken ct)
    {
        // Scalar-Felder der Fraktion (Soft-Delete-Filter blendet gelöschte aus → kein Treffer → kein Recompute).
        var f = await db.Factions
            .Where(x => x.Id == factionId)
            .Select(x => new
            {
                x.IsStateFaction,
                x.Classification,
                x.EstimatedMemberCount,
                x.Estate,
                Captured = x.ModifiedAt ?? x.CreatedAt,
            })
            .FirstOrDefaultAsync(ct);
        if (f is null)
        {
            return;
        }

        ThreatScoreInput input = f.IsStateFaction
            ? new ThreatScoreInput { IsStateFaction = true, Classification = f.Classification }
            : await LoadInputAsync(db, factionId, f.Classification, f.EstimatedMemberCount,
                !string.IsNullOrWhiteSpace(f.Estate), f.Captured, ct);

        var result = Calculate(input, DateTime.UtcNow, config);
        await PersistFactionAsync(db, factionId, result, ct);
    }

    private static async Task CalculatePersonAsync(AppDbContext db, string personId, ThreatScoreConfiguration config, CancellationToken ct)
    {
        var p = await db.People
            .Where(x => x.Id == personId)
            .Select(x => new
            {
                x.Classification,
                x.LifeStatus,
                x.DeadUntil,
                Captured = x.ModifiedAt ?? x.CreatedAt,
            })
            .FirstOrDefaultAsync(ct);
        if (p is null)
        {
            return;
        }

        var input = await LoadPersonInputAsync(db, personId, p.Classification, p.LifeStatus, p.DeadUntil, p.Captured, ct);
        var result = CalculatePerson(input, DateTime.UtcNow, config);
        await PersistPersonAsync(db, personId, result, ct);
    }

    private static async Task<ThreatScoreInput> LoadInputAsync(AppDbContext db, string factionId,
        Classification classification, int? estimated, bool hasEstate, DateTime factionCapturedUtc, CancellationToken ct)
    {
        // Aktive Mitglieder (= nicht ausgetreten; globaler Soft-Delete-Filter greift).
        var activeMembers = await db.FactionMembers
            .Where(m => m.FactionId == factionId)
            .Select(m => new { m.IsLead, m.CreatedAt, m.ModifiedAt })
            .ToListAsync(ct);

        int ranks = await db.FactionRanks.Where(r => r.FactionId == factionId).CountAsync(ct);

        var weapons = await db.FactionWeaponStocks.Where(w => w.FactionId == factionId).Select(w => w.Designation).ToListAsync(ct);
        var stock = await db.FactionInventories.Where(l => l.FactionId == factionId).Select(l => l.Designation).ToListAsync(ct);
        var routes = await db.FactionDrugRoutes.Where(d => d.FactionId == factionId).Select(d => d.Designation).ToListAsync(ct);

        var activitiesRows = await db.FactionActivities
            .Where(a => a.FactionId == factionId)
            .Select(a => new { a.Kind, a.Timestamp, a.CreatedAt, a.ModifiedAt })
            .ToListAsync(ct);
        var activities = activitiesRows.Select(a => new ThreatActivity(a.Kind, a.Timestamp)).ToList();

        // Mitgliedschafts-Perioden inkl. ausgetretener (IgnoreQueryFilters) → austritts-stabiler Heat.
        var periods = await db.FactionMembers.IgnoreQueryFilters()
            .Where(m => m.FactionId == factionId && m.PersonId != "")
            .Select(m => new { m.PersonId, Joined = m.CreatedAt, Departure = m.DeletedAt })
            .ToListAsync(ct);
        var personIds = periods.Select(p => p.PersonId).Distinct().ToList();

        var docRows = personIds.Count == 0
            ? new List<DocRow>()
            : (await db.PersonDocs
                .Where(d => personIds.Contains(d.PersonId))
                .Select(d => new { d.PersonId, d.Outcome, d.Timestamp })
                .ToListAsync(ct))
              .Select(d => new DocRow(d.PersonId, d.Outcome, d.Timestamp))
              .ToList();
        var docsByPerson = docRows.GroupBy(d => d.PersonId).ToDictionary(g => g.Key, g => g.ToList());

        var docsPerMember = new List<IReadOnlyList<ThreatDoc>>();
        foreach (var p in periods)
        {
            if (!docsByPerson.TryGetValue(p.PersonId, out var personDocs))
            {
                continue;
            }
            var until = p.Departure ?? DateTime.MaxValue;
            var inWindow = personDocs
                .Where(d => d.Timestamp >= p.Joined && d.Timestamp <= until)
                .Select(d => new ThreatDoc(d.Outcome, d.Timestamp))
                .ToList();
            if (inWindow.Count > 0)
            {
                docsPerMember.Add(inWindow);
            }
        }

        var kinds = await db.Links
            .Where(v => !v.Automatic
                && (v.Kind == LinkKind.Conflict || v.Kind == LinkKind.Alliance)
                && ((v.SourceType == nameof(Faction) && v.SourceId == factionId)
                 || (v.TargetType == nameof(Faction) && v.TargetId == factionId)))
            .Select(v => v.Kind)
            .ToListAsync(ct);

        var defaultEdgesDegree = await db.Links
            .Where(v => !v.Automatic
                && v.Kind == LinkKind.Default
                && ((v.SourceType == nameof(Faction) && v.SourceId == factionId)
                 || (v.TargetType == nameof(Faction) && v.TargetId == factionId)))
            .CountAsync(ct);

        DateTime? latest = factionCapturedUtc;
        foreach (var a in activitiesRows)
        {
            latest = Later(latest, a.ModifiedAt ?? a.CreatedAt);
        }
        foreach (var m in activeMembers)
        {
            latest = Later(latest, m.ModifiedAt ?? m.CreatedAt);
        }
        var lastClassification = await db.ClassificationHistory
            .Where(ev => ev.EntityType == nameof(Faction) && ev.EntityId == factionId)
            .OrderByDescending(ev => ev.Timestamp)
            .Select(ev => (DateTime?)ev.Timestamp)
            .FirstOrDefaultAsync(ct);
        latest = Later(latest, lastClassification);

        return new ThreatScoreInput
        {
            IsStateFaction = false,
            Classification = classification,
            EstimatedMemberCount = estimated,
            ActiveMembersCount = activeMembers.Count,
            HasActiveLead = activeMembers.Any(m => m.IsLead),
            RanksCount = ranks,
            HasEstate = hasEstate,
            DistinctWeaponsCount = DistinctNotEmpty(weapons),
            InventoryCount = DistinctNotEmpty(stock),
            DrugRoutesCount = DistinctNotEmpty(routes),
            Activities = activities,
            DocsPerMember = docsPerMember,
            ConflictCount = kinds.Count(a => a == LinkKind.Conflict),
            AllianceCount = kinds.Count(a => a == LinkKind.Alliance),
            DefaultEdgesDegree = defaultEdgesDegree,
            LatestCaptureUtc = latest,
        };
    }

    private static async Task<PersonThreatScoreInput> LoadPersonInputAsync(AppDbContext db, string personId,
        Classification classification, LifeStatus lifeStatus, DateTime? deadUntil, DateTime personCapturedUtc, CancellationToken ct)
    {
        var docRows = await db.PersonDocs
            .Where(d => d.PersonId == personId)
            .Select(d => new { d.Outcome, d.Timestamp, d.CreatedAt, d.ModifiedAt })
            .ToListAsync(ct);
        var docs = docRows.Select(d => new ThreatDoc(d.Outcome, d.Timestamp)).ToList();

        var weapons = await db.PersonWeapons.Where(w => w.PersonId == personId).Select(w => w.Text).ToListAsync(ct);

        var obsRows = await db.Observations
            .Where(o => o.PersonId == personId)
            .Select(o => new { o.Start, o.End, o.CreatedAt, o.ModifiedAt })
            .ToListAsync(ct);
        var observations = obsRows.Select(o => new ThreatObservation(o.Start, o.End)).ToList();

        var relationTypes = await db.PersonRelations
            .Where(b => b.PersonAId == personId || b.PersonBId == personId)
            .Select(b => b.Type)
            .ToListAsync(ct);

        // Leitungsrollen + Mitgliedschaften über alle drei Mitglied-Tabellen (aktiv = Soft-Delete-Filter).
        int leadFr = await db.FactionMembers.Where(m => m.PersonId == personId && m.IsLead).CountAsync(ct);
        int leadGr = await db.PersonGroupMembers.Where(m => m.PersonId == personId && m.IsLead).CountAsync(ct);
        int leadPa = await db.PartyMembers.Where(m => m.PersonId == personId && m.IsLead).CountAsync(ct);
        int memberFr = await db.FactionMembers.Where(m => m.PersonId == personId).CountAsync(ct);
        int memberGr = await db.PersonGroupMembers.Where(m => m.PersonId == personId).CountAsync(ct);
        int memberPa = await db.PartyMembers.Where(m => m.PersonId == personId).CountAsync(ct);

        var defaultEdgesDegree = await db.Links
            .Where(v => !v.Automatic
                && v.Kind == LinkKind.Default
                && ((v.SourceType == nameof(Person) && v.SourceId == personId)
                 || (v.TargetType == nameof(Person) && v.TargetId == personId)))
            .CountAsync(ct);

        int aliases = await db.PersonAliases.Where(a => a.PersonId == personId).CountAsync(ct);
        int vehicles = await db.PersonVehicles.Where(f => f.PersonId == personId).CountAsync(ct);
        int phones = await db.PersonPhones.Where(t => t.PersonId == personId).CountAsync(ct);
        int locations = await db.PersonLocations.Where(o => o.PersonId == personId).CountAsync(ct);

        // Jüngste *Erfassung* (Erfassungszeit, nicht RP-Zeit): Person + jüngstes Dok/Observation.
        DateTime? latest = personCapturedUtc;
        foreach (var d in docRows)
        {
            latest = Later(latest, d.ModifiedAt ?? d.CreatedAt);
        }
        foreach (var o in obsRows)
        {
            latest = Later(latest, o.ModifiedAt ?? o.CreatedAt);
        }

        return new PersonThreatScoreInput
        {
            Classification = classification,
            LifeStatus = lifeStatus,
            DeadUntil = deadUntil,
            Docs = docs,
            DistinctWeaponsCount = DistinctNotEmpty(weapons),
            Observations = observations,
            EnemyCount = relationTypes.Count(t => t == RelationType.Enemy),
            AllyCount = relationTypes.Count(t => t == RelationType.Ally),
            BusinessPartnerCount = relationTypes.Count(t => t == RelationType.BusinessPartner),
            LeadershipRolesCount = leadFr + leadGr + leadPa,
            DefaultEdgesDegree = defaultEdgesDegree,
            MembershipsCount = memberFr + memberGr + memberPa,
            DataRichness = aliases + vehicles + phones + locations,
            LatestCaptureUtc = latest,
        };
    }

    private static async Task PersistFactionAsync(AppDbContext db, string factionId, ThreatScoreResult erg, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(erg.Detail, JsonOptions);
        // Bewusst per ExecuteUpdate am Audit-Interceptor vorbei: kein GeaendertAm-Stempel (sonst verfälschte
        // Aktualitäts-Ampel) und keine AuditLog-Zeile je Recompute. Der Soft-Delete-Filter greift weiterhin.
        await db.Factions
            .Where(f => f.Id == factionId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.ThreatScore, erg.Score)
                .SetProperty(f => f.ThreatConfidence, erg.Confidence)
                .SetProperty(f => f.ThreatDetailJson, json)
                .SetProperty(f => f.ScoreCalculatedAt, erg.Detail.CalculatedAtUtc), ct);
    }

    private static async Task PersistPersonAsync(AppDbContext db, string personId, ThreatScoreResult erg, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(erg.Detail, JsonOptions);
        await db.People
            .Where(p => p.Id == personId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.ThreatScore, erg.Score)
                .SetProperty(p => p.ThreatConfidence, erg.Confidence)
                .SetProperty(p => p.ThreatDetailJson, json)
                .SetProperty(p => p.ScoreCalculatedAt, erg.Detail.CalculatedAtUtc), ct);
    }

    private static int DistinctNotEmpty(IEnumerable<string> values) => values
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Select(s => s.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    private static DateTime? Later(DateTime? a, DateTime? b) => (a, b) switch
    {
        (null, _) => b,
        (_, null) => a,
        _ => a.Value >= b.Value ? a : b,
    };

    private readonly record struct DocRow(string PersonId, MeasureOutcome Outcome, DateTime Timestamp);
}
