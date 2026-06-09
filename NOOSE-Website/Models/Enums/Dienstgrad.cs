namespace NOOSE_Website.Models.Enums;

/// <summary>
/// NOOSE-Dienstgrade in aufsteigender Reihenfolge. Die Fuehrung beginnt ab
/// <see cref="SupervisorySpecialAgent"/>. Die Einstufung "Gesichert staatsgefaehrdend"
/// darf ab <see cref="SeniorSpecialAgent"/> eigenstaendig vergeben werden (siehe Plan.md, Phase 1/2).
/// </summary>
public enum Dienstgrad
{
    JuniorAgent = 1,
    SpecialAgent = 2,
    SeniorSpecialAgent = 3,
    SupervisorySpecialAgent = 4, // ab hier Fuehrung
    DeputyDirector = 5,
    Director = 6,
}
