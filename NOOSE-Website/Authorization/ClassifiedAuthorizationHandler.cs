using Microsoft.AspNetCore.Authorization;

namespace NOOSE_Website.Authorization;

/// <summary>
/// Handler für <see cref="VerschlusssacheRequirement"/>: lässt Führung/Admin durch.
/// <para>
/// Ist-Zustand: Die eigentliche Verschlusssachen-Durchsetzung erfolgt seit den Akten-Phasen serverseitig in
/// der Service-Schicht (zentral über <c>Sichtbarkeit.IstAkteSichtbarAsync</c> und die VS-Guards der
/// schreibenden Methoden) – nicht über diese derzeit ungenutzte Policy. Sie ist als künftiges,
/// ressourcenbasiertes UI-Gate vorgesehen (konkrete Akte als Resource: als Verschlusssache markiert? Agent
/// ausdrücklich zugewiesen?), sobald ein Zuweisungs-Datenmodell existiert.
/// </para>
/// </summary>
public class ClassifiedAuthorizationHandler : AuthorizationHandler<ClassifiedRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ClassifiedRequirement requirement)
    {
        if (context.User.IsLeadership())
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
