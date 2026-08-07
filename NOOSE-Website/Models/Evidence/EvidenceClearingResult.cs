namespace NOOSE_Website.Models.Evidence;

/// <summary>Outcome of one clearing run: what was booked out, what was corrected up, what was skipped.</summary>
public record EvidenceClearingResult(
    int ClearedItems,
    int ClearedPieces,
    int CorrectedItems,
    int CorrectedPieces,
    int SkippedItems,
    string? WithdrawalEntryId = null,
    string? WithdrawalCaseNumber = null,
    string? CorrectionEntryId = null,
    string? CorrectionCaseNumber = null)
{
    public static readonly EvidenceClearingResult Empty = new(0, 0, 0, 0, 0);

    /// <summary>True when at least one entry was booked.</summary>
    public bool Booked => ClearedItems > 0 || CorrectedItems > 0;
}
