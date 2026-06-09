using Microsoft.AspNetCore.Authorization;

namespace NOOSE_Website.Authorization;

/// <summary>
/// Sichtbarkeit von Verschlusssachen. In Phase 1 nur vorbereitet: der zugehoerige Handler
/// laesst aktuell Fuehrung/Admin durch. Die volle ressourcenbasierte Pruefung (markierte Akte
/// bzw. ausdruecklich zugewiesene Agenten) folgt, sobald Akten existieren.
/// </summary>
public class VerschlusssacheRequirement : IAuthorizationRequirement
{
}
