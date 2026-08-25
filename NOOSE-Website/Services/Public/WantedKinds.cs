using System.Linq.Expressions;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services.Public;

/// <summary>Which kinds of notice describe a thing rather than a person; the one place that draws the line.</summary>
/// <remarks>
/// Board filter, duplicate check and the file panel all read this. Spelled out at each site it drifts, and a kind on
/// the wrong side of the line decides both which module switch owns it and whether it may carry a photo.
/// <para>
/// Vermisst and Zeugenaufruf sit on the person side: nobody issues them yet, but both are statements about a human.
/// </para>
/// </remarks>
public static class WantedKinds
{
    /// <summary>True for a notice about a vehicle or a weapon.</summary>
    public static bool IsItem(PublicWantedKind kind)
        => kind is PublicWantedKind.Fahrzeug or PublicWantedKind.Waffe;

    /// <summary>Query twin of <see cref="IsItem"/>.</summary>
    public static readonly Expression<Func<OeffentlicheFahndung, bool>> ItemRows =
        f => f.Kind == PublicWantedKind.Fahrzeug || f.Kind == PublicWantedKind.Waffe;

    /// <summary>Query twin of the person side; the complement of <see cref="ItemRows"/>.</summary>
    public static readonly Expression<Func<OeffentlicheFahndung, bool>> PersonRows =
        f => f.Kind != PublicWantedKind.Fahrzeug && f.Kind != PublicWantedKind.Waffe;
}
