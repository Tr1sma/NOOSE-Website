using System.Security.Claims;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Authorization;

/// <summary>Document-library classification visibility for a viewer, so services can filter levels server-side without per-request claim eval.</summary>
public readonly record struct DocumentViewerScope(bool MayClassified, bool IsTru, bool IsHrb, bool IsLeadership, string? MeId)
{
    /// <summary>Derives the visibility scope from the signed-in agent's claims.</summary>
    public static DocumentViewerScope From(ClaimsPrincipal user)
        => new(user.MayClassifiedRead(), user.IsTRU(), user.IsHRB(), user.IsLeadership(), user.GetAgentId());

    public bool CanSee(DocumentClassification classification) => classification switch
    {
        DocumentClassification.None => true,
        DocumentClassification.Leadership => MayClassified,
        DocumentClassification.Tru => MayClassified || IsTru,
        DocumentClassification.Hrb => MayClassified || IsHrb,
        _ => false,
    };

    /// <summary>Classification levels the agent may assign in the editor/upload dialog.</summary>
    public static IReadOnlyList<DocumentClassification> AssignableOptions(ClaimsPrincipal user)
    {
        var options = new List<DocumentClassification> { DocumentClassification.None };
        if (user.IsLeadership())
        {
            options.Add(DocumentClassification.Leadership);
        }
        if (user.IsLeadership() || user.IsTRU())
        {
            options.Add(DocumentClassification.Tru);
        }
        if (user.IsLeadership() || user.IsHRB())
        {
            options.Add(DocumentClassification.Hrb);
        }
        return options;
    }
}
