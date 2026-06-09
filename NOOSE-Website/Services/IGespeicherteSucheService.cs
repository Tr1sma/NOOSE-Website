using System.Security.Claims;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>Gespeicherte Suchen/Smart-Listen je Agent (anlegen, eigene laden, eigene löschen).</summary>
public interface IGespeicherteSucheService
{
    Task<List<GespeicherteSuche>> GetFuerAgentAsync(string agentId, CancellationToken cancellationToken = default);

    Task<GespeicherteSuche> SpeichernAsync(string agentId, string name, SuchKriterien kriterien, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task LoeschenAsync(string id, string agentId, CancellationToken cancellationToken = default);
}
