namespace NOOSE_Website.Services;

/// <summary>Closes aptitude-test attempts whose processing time ran out while nobody was watching.</summary>
/// <remarks>
/// Its own interface rather than a member of <see cref="IBewerbungTestService"/>: there is no actor here, and
/// every member of that interface carries exactly one actor guard. Mirrors how the public wanted expiry is
/// separated from the acting service.
/// </remarks>
public interface IBewerbungTestExpiryService
{
    /// <summary>Hands in every overdue attempt as it stands; returns how many were closed.</summary>
    Task<int> ExpireDueAsync(CancellationToken cancellationToken = default);
}
