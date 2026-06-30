namespace NOOSE_Website.Authorization;

/// <summary>Relative routes a partner may open; everything else is blocked centrally (MainLayout/PrintLayout).</summary>
public static class PartnerRoutes
{
    private static readonly string[] AllowedPrefixes =
    {
        "personen",
        "fraktionen",
        "personengruppen",
        "parteien",
        "operationen",
        "vorgaenge",
        "taskforces",
        "dokumente",
        "gesetze",
        "suche",
    };

    private static readonly string[] BlockedSuffixes = { "/neu", "/bearbeiten", "/papierkorb" };

    // create routes a partner may open despite the blanket create-block (document authoring is universal)
    private static readonly string[] AuthoringRoutes = { "dokumente/neu" };

    /// <summary>True if a partner may open this relative path (dashboard and own profile always allowed).</summary>
    public static bool IsAllowed(string? relativePath)
    {
        var path = Normalize(relativePath);
        if (path.Length == 0 || path == "dashboard" || path == "profil" || path.StartsWith("profil/"))
        {
            return true;
        }
        if (AuthoringRoutes.Contains(path))
        {
            return true;
        }
        if (BlockedSuffixes.Any(s => ("/" + path).EndsWith(s)))
        {
            return false;
        }
        return AllowedPrefixes.Any(p => path == p || path.StartsWith(p + "/"));
    }

    /// <summary>Strips query/fragment, trims slashes, lowercases.</summary>
    private static string Normalize(string? relativePath)
        => (relativePath ?? string.Empty).Split('?')[0].Split('#')[0].Trim('/').ToLowerInvariant();
}
