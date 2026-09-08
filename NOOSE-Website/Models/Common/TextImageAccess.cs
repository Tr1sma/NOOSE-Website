namespace NOOSE_Website.Models.Common;

/// <summary>What the delivery endpoint may know about a pasted image: the file and its type, never the carrying record.</summary>
public sealed record TextImageAccess(string FileNameSaved, string ContentType);
