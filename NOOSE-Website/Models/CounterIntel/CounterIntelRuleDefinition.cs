using System.Text.Json;
using System.Text.Json.Serialization;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.CounterIntel;

/// <summary>
/// The combinable condition set of one counter-intelligence rule, stored as JSON on the rule row.
/// </summary>
/// <remarks>
/// Combination semantics, uniform across every list: within a category OR, between categories AND,
/// empty list means no restriction. That covers the useful space without an expression parser.
/// </remarks>
public sealed class CounterIntelRuleDefinition
{
    public const int MaxWindowDays = 90;
    public const int MaxSlidingMinutes = 1440;

    /// <summary>Serializer shared by the service and the seeded defaults.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ---- observation window ----

    /// <summary>How far back the rule looks, in days.</summary>
    public int WindowDays { get; set; } = 30;

    // ---- event ----

    /// <summary>Matching actions; empty = every action.</summary>
    public List<CounterIntelActionKind> Actions { get; set; } = [];

    // ---- target ----

    /// <summary>Matching record types as CLR type names; empty = every type.</summary>
    public List<string> EntityTypes { get; set; } = [];

    /// <summary>Matching record ids; empty = every record.</summary>
    public List<string> EntityIds { get; set; } = [];

    /// <summary>null = any, true = classified only, false = unclassified only.</summary>
    public bool? ClassifiedOnly { get; set; }

    /// <summary>Matching target classifications; empty = every classification.</summary>
    public List<Classification> Classifications { get; set; } = [];

    /// <summary>Target carries one of these tags; empty = no tag condition.</summary>
    public List<string> TagIds { get; set; } = [];

    // ---- actor ----

    /// <summary>Matching ranks; empty = every rank.</summary>
    public List<Rank> ActorRanks { get; set; } = [];

    /// <summary>Only these agents; empty = every agent.</summary>
    public List<string> ActorIds { get; set; } = [];

    /// <summary>Never these agents.</summary>
    public List<string> ExcludedActorIds { get; set; } = [];

    /// <summary>null = any, true = must carry the flag, false = must not.</summary>
    public bool? RequireTru { get; set; }

    /// <inheritdoc cref="RequireTru" />
    public bool? RequireHrb { get; set; }

    /// <inheritdoc cref="RequireTru" />
    public bool? RequireAdmin { get; set; }

    public CounterIntelPartnerScope PartnerScope { get; set; } = CounterIntelPartnerScope.Any;

    /// <summary>null = any, true = actor and target share an organisation, false = they must not.</summary>
    /// <remarks>
    /// Resolved over the civilian profile of the acting account: its linked person file against the person behind the
    /// target. The default must stay null — the seeded rules carry their JSON as a literal from the migration and do
    /// not know this property, so it deserializes to the C# default there and must change no existing rule.
    /// </remarks>
    public bool? ActorSharesOrgWithTarget { get; set; }

    // ---- time of day ----

    /// <summary>Start of the daily window; equal to <see cref="ToHour"/> means all day.</summary>
    public int FromHour { get; set; }

    /// <summary>End of the daily window, exclusive; may wrap past midnight (22 → 6).</summary>
    public int ToHour { get; set; }

    /// <summary>Matching weekdays; empty = every day.</summary>
    public List<DayOfWeek> Weekdays { get; set; } = [];

    // ---- trigger ----

    public CounterIntelCountMode CountMode { get; set; } = CounterIntelCountMode.Events;

    public CounterIntelBucket Bucket { get; set; } = CounterIntelBucket.Window;

    /// <summary>Span of the sliding bucket in minutes; only read when Bucket is Sliding.</summary>
    public int SlidingMinutes { get; set; } = 60;

    /// <summary>Count at or above which the rule flags.</summary>
    public int Threshold { get; set; } = 10;

    /// <summary>True when the daily window covers the whole day.</summary>
    [JsonIgnore]
    public bool IsAllDay => FromHour == ToHour;

    /// <summary>True when any condition needs the target record looked up.</summary>
    [JsonIgnore]
    public bool NeedsTargetLookup => ClassifiedOnly is not null || Classifications.Count > 0;

    /// <summary>True when any condition needs the target's tags looked up.</summary>
    [JsonIgnore]
    public bool NeedsTagLookup => TagIds.Count > 0;

    /// <summary>True when any condition needs the organisations of actor and target.</summary>
    [JsonIgnore]
    public bool NeedsOrgLookup => ActorSharesOrgWithTarget is not null;

    /// <summary>True when any condition needs the actor's roster row.</summary>
    [JsonIgnore]
    public bool NeedsActorLookup =>
        ActorRanks.Count > 0 || RequireTru is not null || RequireHrb is not null
        || RequireAdmin is not null || PartnerScope != CounterIntelPartnerScope.Any;

    /// <summary>Deserializes a stored definition; returns null when the JSON is unusable.</summary>
    public static CounterIntelRuleDefinition? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<CounterIntelRuleDefinition>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Serializes for storage.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Deep copy, so editing a dialog copy never mutates the loaded rule.</summary>
    public CounterIntelRuleDefinition Clone() => TryParse(ToJson()) ?? new CounterIntelRuleDefinition();
}
