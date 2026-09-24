namespace NOOSE_Website.Models.Common;

/// <summary>What one batch write did with each record: written, already in place, or refused.</summary>
public sealed record BatchOutcome(int Done, int Unchanged, int Refused)
{
    public int Total => Done + Unchanged + Refused;
}
