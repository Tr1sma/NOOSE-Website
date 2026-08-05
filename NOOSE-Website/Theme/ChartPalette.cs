using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Theme;

/// <summary>Single source of truth for chart colours; every value validated against the dark surface #161B22.</summary>
/// <remarks>
/// Each palette does exactly one job. Never mix them: a status colour must not stand in for a series,
/// and a series colour must not imply "good" or "critical".
///
/// The theme accent colours are deliberately NOT used as series colours. Against the dark surface they
/// sit above the usable lightness band (OKLCH L ~0.68-0.80 vs 0.48-0.67) and TextSecondary falls below
/// the chroma floor, so they read as gray. The palettes below are the same hue families stepped into the
/// band, then checked for colour-blind separation. The admin's runtime accent override stays UI chrome
/// (buttons, rail, active states) because an arbitrary hex would destroy the validated distances.
/// </remarks>
public static class ChartPalette
{
    /// <summary>Series identity. Fixed order — assign in sequence, never cycle, never generate a ninth.</summary>
    /// <remarks>
    /// cyan, orange, indigo, amber, violet, green, magenta, red. Worst adjacent pair: colour-blind
    /// dE 13.8, normal-vision dE 16.0, every step at least 3.5:1 on the surface.
    /// </remarks>
    public static readonly string[] Categorical =
    [
        "#18a2b7", "#d86b00", "#5f6bb8", "#bb8603",
        "#7b64ab", "#3aab4a", "#ab4f91", "#e85d53",
    ];

    /// <summary>Series cap for charts where any two marks can end up side by side (scatter, bubble, small multiples).</summary>
    /// <remarks>Only the first three slots survive the all-pairs check; past that, fold into "Sonstige" or facet.</remarks>
    public const int AllPairsSeriesCap = 3;

    /// <summary>Magnitude, low to high. One hue family, brighter means more.</summary>
    /// <remarks>Monotone lightness with every gap above the visible-step threshold; use for every heatmap.</remarks>
    public static readonly string[] Sequential =
    [
        "#006270", "#037787", "#0d8c9f", "#17a2b7", "#04bad2", "#14d1eb",
    ];

    /// <summary>Polarity around a baseline: cool arm, neutral middle, warm arm.</summary>
    /// <remarks>
    /// The midpoint is the theme's LinesInputs — near-gray, so it reads as "nothing". The inner steps
    /// sit just under 3:1, so diverging marks always carry visible value labels.
    /// </remarks>
    public static readonly string[] Diverging =
    [
        "#17a2b7", "#0a8b9e", "#007585", "#2B3744", "#9f4e03", "#be5d00", "#dc6e07",
    ];

    /// <summary>Neutral middle of <see cref="Diverging"/>; also the "no data" tint.</summary>
    public const string DivergingNeutral = "#2B3744";

    /// <summary>Cool (negative / falling) end of the diverging scale.</summary>
    public const string DivergingCool = "#17a2b7";

    /// <summary>Warm (positive / rising) end of the diverging scale.</summary>
    public const string DivergingWarm = "#dc6e07";

    /// <summary>Hazard heat, indexed by <see cref="HazardLevel"/>. Reserved meaning — never a series colour.</summary>
    /// <remarks>Unchanged from the established scale so the graph, calendar and hazard lists keep one legend.</remarks>
    public static readonly string[] Hazard =
    [
        "#9BA8B8", "#3FB950", "#22D3EE", "#D29922", "#F85149",
    ];

    /// <summary>De-emphasis gray for context marks behind an emphasised series.</summary>
    public const string Muted = "#5A6677";

    /// <summary>Chart surface; also what a zero heatmap cell shows instead of the lightest ramp step.</summary>
    public const string Surface = "#161B22";

    /// <summary>Hairline for gridlines, axes and cell borders.</summary>
    public const string Gridline = "#222B36";

    /// <summary>Hazard colour for a level, safe against out-of-range values.</summary>
    public static string ForHazard(HazardLevel level)
    {
        var i = (int)level;
        return i >= 0 && i < Hazard.Length ? Hazard[i] : Hazard[0];
    }

    /// <summary>Hazard colour for a raw score, using the same thresholds as the hazard bands.</summary>
    public static string ForScore(int? score) => ForHazard(HazardLevelLogic.From(score));

    /// <summary>First <paramref name="count"/> categorical slots; order is stable so a filter never repaints survivors.</summary>
    public static string[] Series(int count)
    {
        if (count <= 0)
        {
            return [];
        }
        // A ninth series would need a generated hue, which no colour-blind check can pass
        return Categorical.Take(Math.Min(count, Categorical.Length)).ToArray();
    }

    /// <summary>Ordinal ramp of <paramref name="count"/> steps for an ordered scale (bands, funnel stages, tiers).</summary>
    /// <remarks>Spreads across the sequential ramp so the reader sees the order in the colour itself.</remarks>
    public static string[] Ordinal(int count)
    {
        if (count <= 0)
        {
            return [];
        }
        if (count == 1)
        {
            return [Sequential[^1]];
        }
        var steps = new string[count];
        for (var i = 0; i < count; i++)
        {
            // walk the whole ramp so the endpoints are always the extremes
            var position = (double)i / (count - 1) * (Sequential.Length - 1);
            steps[i] = Sequential[(int)Math.Round(position)];
        }
        return steps;
    }

    /// <summary>Sequential step for a value in [0, max]; returns null at zero so the cell stays surface-coloured.</summary>
    public static string? ForMagnitude(int value, int max)
    {
        if (value <= 0 || max <= 0)
        {
            return null;
        }
        var position = (double)value / max * (Sequential.Length - 1);
        return Sequential[Math.Clamp((int)Math.Ceiling(position), 0, Sequential.Length - 1)];
    }
}
