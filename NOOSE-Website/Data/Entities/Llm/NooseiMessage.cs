using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Llm;

/// <summary>One wire message of a conversation, including tool calls and their results, so a turn can be replayed.</summary>
[Table("KiNachrichten")]
public class NooseiMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("UnterhaltungId")]
    public string ConversationId { get; set; } = string.Empty;
    public NooseiConversation? Conversation { get; set; }

    /// <summary>Explicit order; timestamps within one turn are not distinguishable.</summary>
    [Column("Reihenfolge")]
    public int Sequence { get; set; }

    /// <summary>user | assistant | tool — stored as text so the table stays readable.</summary>
    [Column("Rolle")]
    public string Role { get; set; } = string.Empty;

    [Column("Inhalt")]
    public string? Content { get; set; }

    [Column("WerkzeugAufrufeJson")]
    public string? ToolCallsJson { get; set; }

    [Column("WerkzeugAufrufId")]
    public string? ToolCallId { get; set; }

    [Column("WerkzeugName")]
    public string? ToolName { get; set; }

    [Column("KontingentTokens")]
    public long? QuotaTokens { get; set; }

    [Column("IstFehler")]
    public bool IsError { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
}
