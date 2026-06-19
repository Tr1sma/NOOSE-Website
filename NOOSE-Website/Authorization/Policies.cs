namespace NOOSE_Website.Authorization;

/// <summary>Name constants for all authorization policies.</summary>
public static class Policies
{
    /// <summary>Signed in with status Active. App-wide default.</summary>
    public const string ActiveAgent = "AktiverAgent";

    /// <summary>Leadership: rank ≥ Supervisory Special Agent or admin.</summary>
    public const string Leadership = "Fuehrung";

    /// <summary>Technical system role.</summary>
    public const string Admin = "Admin";

    /// <summary>May write at all (everyone except read-only supervision). For mutation controls in AuthorizeView.</summary>
    public const string WriteAccess = "Schreibrecht";

    /// <summary>Read-only supervision active. For the global read-only banner.</summary>
    public const string OnlyReadMode = "NurLeseModus";

    /// <summary>External partner (DoJ/LSPD/LSMD): reduced read-only navigation and views.</summary>
    public const string PartnerView = "PartnerAnsicht";

    /// <summary>Internal NOOSE agent (not a partner): full navigation and internal pages.</summary>
    public const string InternalAgent = "InternerAgent";

    /// <summary>Leadership page access: leadership OR read-only supervision (opens read-only; write buttons stay on Leadership).</summary>
    public const string LeadershipPage = "FuehrungSeite";

    /// <summary>Highest-classification page access: like HighestClassification plus read-only supervision.</summary>
    public const string HighestClassificationPage = "HoechsteEinstufungSeite";

    /// <summary>Admin page access: admin OR read-only supervision (opens read-only).</summary>
    public const string AdminPage = "AdminSeite";

    /// <summary>Set "secured state-threatening" directly: rank ≥ Senior Special Agent or admin.</summary>
    public const string HighestClassification = "HoechsteEinstufung";

    /// <summary>Decide promotions: rank ≥ Deputy Director or admin.</summary>
    public const string PromotionDecide = "BefoerderungEntscheiden";

    /// <summary>Applicant portal access: signed in with status Applicant.</summary>
    public const string ApplicantPortal = "BewerberPortal";

    /// <summary>Recruiting management access: HRB member or leadership.</summary>
    public const string HrbOrLeadership = "HrbOderFuehrung";
}
