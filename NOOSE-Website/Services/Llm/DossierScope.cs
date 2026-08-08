using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>The scope a cached dossier brief is generated at.</summary>
public static class DossierScope
{
    /// <summary>Exactly the audience of the root record, nothing wider.</summary>
    /// <remarks>
    /// A brief is cached once per record, so it cannot be assembled at the reader's own scope without either
    /// leaking to the narrowest reader or exploding into one row per viewer. Generating at minimum privilege
    /// keeps a single row safe for everyone who may open the record. Accepted trade-off: a leadership reader's
    /// brief may omit a link they personally could see — the record page still shows it.
    /// </remarks>
    public static ViewerScope ForRecord(DocumentClassification rootLevel)
        => new(
            MayClassifiedRead: rootLevel == DocumentClassification.Leadership,
            MayAllTaskforces: false,
            MeId: null,
            PartnerAgency: null,
            IsTru: rootLevel == DocumentClassification.Tru,
            IsHrb: rootLevel == DocumentClassification.Hrb);

    /// <summary>Secrecy level of a linked record from its three flags; mirrors the gate in <see cref="Visibility"/>.</summary>
    public static DocumentClassification LevelOf(bool classified, bool tru, bool hrb)
        => !classified ? DocumentClassification.None
            : tru ? DocumentClassification.Tru
            : hrb ? DocumentClassification.Hrb
            : DocumentClassification.Leadership;
}
