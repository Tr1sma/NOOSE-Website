using System.Security.Claims;
using NOOSE_Website.Data.Entities.Kasse;
using NOOSE_Website.Models.Kasse;

namespace NOOSE_Website.Services;

/// <summary>Management of recurring-booking templates; writes are leadership-only.</summary>
public interface IKassenTemplateService
{
    /// <summary>All templates for management, sorted.</summary>
    Task<List<KassenBuchungVorlage>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Active templates only for the quick-book bar.</summary>
    Task<List<KassenBuchungVorlage>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<KassenBuchungVorlage?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<KassenBuchungVorlage> CreateAsync(KassenVorlageInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RefreshAsync(string id, KassenVorlageInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
