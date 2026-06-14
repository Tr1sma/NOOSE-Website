namespace NOOSE_Website.Models.Enums;

/// <summary>
/// NOOSE-Dienstgrade in aufsteigender Reihenfolge. Die Führung beginnt ab
/// <see cref="SupervisorySpecialAgent"/>. Die Einstufung "Gesichert staatsgefährdend"
/// darf ab <see cref="SeniorSpecialAgent"/> eigenständig vergeben werden (siehe Plan.md, Phase 1/2).
/// </summary>
public enum Rank
{
    JuniorAgent = 1,
    SpecialAgent = 2,
    SeniorSpecialAgent = 3,
    SupervisorySpecialAgent = 4, // ab hier Führung
    DeputyDirector = 5,
    Director = 6,
}
