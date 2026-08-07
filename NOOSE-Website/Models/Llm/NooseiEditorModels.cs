using System.Security.Claims;

namespace NOOSE_Website.Models.Llm;

/// <summary>Which of the two editor actions the agent asked for.</summary>
public enum NooseiMode
{
    /// <summary>Spelling and clear grammar errors only; formatting and meaning stay untouched.</summary>
    Correct = 0,

    /// <summary>NOOSEI writes the text from an instruction.</summary>
    Compose = 1,
}

/// <summary>What the editor hands the dialog when a NOOSEI toolbar button was pressed.</summary>
public sealed record NooseiRequest(
    NooseiMode Mode,
    string Html,
    TextAssistContext Context,
    ClaimsPrincipal User,
    bool HasSelection,
    string? Subject = null,
    string? SurroundingText = null);

/// <summary>What the agent accepted, on its way back into the editor.</summary>
public sealed record NooseiChoice(string Html, long QuotaTokens, LlmQuotaStatus Quota, bool InsertAtCaret);
