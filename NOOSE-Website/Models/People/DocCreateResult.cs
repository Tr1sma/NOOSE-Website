namespace NOOSE_Website.Models.People;

/// <summary>Result of the cross-record "new doc" dialog; exactly one of PersonId or NewName is set.</summary>
public class DocCreateResult
{
    public string? PersonId { get; init; }

    public string? NewName { get; init; }

    public PersonDocInput Input { get; init; } = new();
}
