using NOOSE_Website.Models.Evidence;

namespace NOOSE_Website.Services;

/// <summary>Display helpers for evidence ledger rows.</summary>
public static class EvidenceDisplay
{
    /// <summary>Positions shown before the summary collapses; a clearing can carry dozens.</summary>
    private const int SummaryMax = 4;

    /// <summary>Short position line for a ledger table cell, e.g. "3× Pistole, 1× Messer … (+57 weitere)".</summary>
    public static string ItemsSummary(EvidenceEntryDisplay entry)
    {
        if (entry.Lines.Count == 0)
        {
            return "—";
        }
        var head = string.Join(", ", entry.Lines.Take(SummaryMax).Select(l => $"{l.Quantity}× {l.ItemName}"));
        return entry.Lines.Count > SummaryMax
            ? $"{head} … (+{entry.Lines.Count - SummaryMax} weitere)"
            : head;
    }

    /// <summary>Every position, for the tooltip behind the truncated summary.</summary>
    public static string ItemsFull(EvidenceEntryDisplay entry)
        => entry.Lines.Count == 0
            ? "—"
            : string.Join(", ", entry.Lines.Select(l => $"{l.Quantity}× {l.ItemName}"));
}
