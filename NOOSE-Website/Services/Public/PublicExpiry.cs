namespace NOOSE_Website.Services.Public;

/// <summary>What a picked expiry date means for a public row.</summary>
/// <remarks>
/// One rule for every outward table that can expire: a picked day is valid <em>through</em> that day, so it becomes
/// the last tick of it in the operator's own time zone and is stored as UTC. Written out twice the two tables would
/// drift, and a warning that dies at midday reads like a bug rather than a decision.
/// </remarks>
public static class PublicExpiry
{
    public static DateTime? From(DateTime? picked) => picked switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } value => value,
        { } value => DateTime.SpecifyKind(value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime(),
    };
}
