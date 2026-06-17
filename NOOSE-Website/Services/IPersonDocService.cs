using System.Security.Claims;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.People;

namespace NOOSE_Website.Services;

/// <summary>Manages person docs; the measure outcome drives the person's life status (shot, amnesty injection).</summary>
public interface IPersonDocService
{
    /// <summary>Docs of a person with resolved (visibility-filtered) link display; partner-filtered when scope is a partner.</summary>
    Task<List<PersonDocDisplay>> GetForPersonAsync(string personId, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>All docs incl. owning person and resolved link; classified-filtered.</summary>
    Task<List<PersonDocDisplay>> GetAllAsync(bool isLeadership, CancellationToken cancellationToken = default);

    /// <summary>Docs linked to an org (faction/group); visibility-filtered, partner-filtered when scope is a partner.</summary>
    Task<List<PersonDocDisplay>> GetForOrgAsync(string orgType, string orgId, ViewerScope scope, CancellationToken cancellationToken = default);

    Task<PersonDoc> CreateAsync(string personId, PersonDocInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Creates a doc for a new person, creating the record (name only) first.</summary>
    Task<PersonDoc> CreateForNewPersonAsync(string name, PersonDocInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Edits a doc; re-applies the outcome to life status only if the active death window came from this doc.</summary>
    Task<PersonDoc> RefreshAsync(string docId, PersonDocInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string docId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
