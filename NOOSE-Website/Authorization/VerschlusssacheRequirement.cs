using Microsoft.AspNetCore.Authorization;

namespace NOOSE_Website.Authorization;

/// <summary>
/// Sichtbarkeit von Verschlusssachen. In Phase 1 nur vorbereitet: der zugehörige Handler
/// lässt aktuell Führung/Admin durch. Die volle ressourcenbasierte Prüfung (markierte Akte
/// bzw. ausdrücklich zugewiesene Agenten) folgt, sobald Akten existieren.
/// </summary>
public class VerschlusssacheRequirement : IAuthorizationRequirement
{
}
