using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Fixed score anchors.</summary>
public static class ThreatScoreConstants
{
    /// <summary>Classification base band.</summary>
    public static int Base(Classification classification) => classification switch
    {
        Classification.SecuredStateThreatening => 75,
        Classification.SuspicionCase => 50,
        Classification.ReviewCase => 12,
        _ => 0, // unknown
    };
}
