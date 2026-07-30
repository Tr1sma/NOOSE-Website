using System.Security.Claims;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Single entry point to every soft-deleted record type.</summary>
public interface ITrashService
{
    /// <summary>Restorable record types, in trash-page order.</summary>
    IReadOnlyList<TrashKind> Kinds { get; }

    /// <summary>Deleted records of one kind, newest deletion first.</summary>
    Task<List<TrashItem>> GetAsync(string kind, CancellationToken cancellationToken = default);

    /// <summary>Restores one record through its own service, so guards and audit still run.</summary>
    Task RestoreAsync(string kind, string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
