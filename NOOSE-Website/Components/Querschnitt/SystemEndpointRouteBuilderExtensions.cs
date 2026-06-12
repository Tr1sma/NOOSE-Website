using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Querschnitt;

/// <summary>
/// Liefert das im Admin hochgeladene Behörden-Logo aus (Phase 7: Theming/Logo-Upload). Bewusst
/// anonym erreichbar – das Logo erscheint bereits in der Topbar vor dem Login und ist nicht
/// schutzwürdig. Ist keines gesetzt, antwortet der Endpoint mit 404 (die UI fällt dann auf das
/// Standard-Wappen <c>NooseIcon.png</c> zurück).
/// </summary>
public static class SystemEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNooseSystemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/system/logo", async (
            [FromServices] ISystemEinstellungService einstellungen,
            CancellationToken cancellationToken) =>
        {
            var logo = await einstellungen.LogoOeffnenAsync(cancellationToken);
            if (logo is null)
            {
                return Results.NotFound();
            }
            // Kein Caching: Der Dateiname im Storage wechselt zwar je Upload, die URL bleibt aber stabil.
            return Results.File(logo.Value.Inhalt, logo.Value.ContentType);
        })
        .AllowAnonymous();
    }
}
