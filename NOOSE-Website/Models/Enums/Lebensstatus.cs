namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Lebensstatus einer Person. Im RP respawnt eine Person nach dem Tod wieder, daher ist „Tot"
/// nur temporär – die effektive Auswertung übernimmt <see cref="LebensstatusLogic"/>.
/// </summary>
public enum Lebensstatus
{
    Lebend = 0,
    Tot = 1,
    Fluechtig = 2,
}
