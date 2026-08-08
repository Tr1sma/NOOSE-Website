namespace NOOSE_Website.Models.Llm;

/// <summary>What kind of text an editor holds. Drives the prompt hint and — for the template editors —
/// which placeholder guard runs, which is why this is not a plain on/off flag.</summary>
public enum TextAssistContext
{
    Document = 0,
    DocumentTemplate = 1,
    Announcement = 2,
    Activity = 3,
    ActivityTemplate = 4,
    MeetingMinutes = 5,
    AgendaNote = 6,
    PersonnelNote = 7,
    PersonnelTemplate = 8,
    Promotion = 9,
    RecruitingTemplate = 10,
}

/// <summary>What one NOOSEI editor action produced.</summary>
public sealed record TextAssistResult(
    string Html,
    string? DiffHtml,
    long QuotaTokens,
    LlmQuotaStatus Quota,
    bool StructureChanged,
    double ChangedRatio,
    bool Unchanged,
    bool DiffDegraded,
    IReadOnlyList<string> Warnings);
