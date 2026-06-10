using System.Security.Claims;
using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Components.Account;

/// <summary>
/// Statische HTTP-Endpoints für den Login-/Logout-Fluss. Diese können NICHT in interaktiven
/// Komponenten leben, weil der OAuth-Ablauf echte HTTP-Redirects (zu Discord und zurück) braucht.
/// </summary>
public static class IdentityComponentsEndpointRouteBuilderExtensions
{
    /// <summary>Name der Rate-Limiting-Policy für den Login-Start (in Program.cs konfiguriert).</summary>
    public const string LoginRateLimitPolicy = "noose-login";

    public static IEndpointConventionBuilder MapNooseAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/Account");

        // 1) Login-Start: fordert den Discord-OAuth-Flow an.
        group.MapPost("/PerformExternalLogin", async (
            [FromServices] SignInManager<Agent> signInManager,
            [FromServices] IAuthenticationSchemeProvider schemeProvider,
            [FromForm] string? returnUrl) =>
        {
            // Falls Discord (noch) nicht konfiguriert ist, existiert das Schema nicht.
            var schema = await schemeProvider.GetSchemeAsync(DiscordAuthenticationDefaults.AuthenticationScheme);
            if (schema is null)
            {
                return RedirectZurLoginSeite(
                    "Discord-Login ist noch nicht konfiguriert. Bitte Client-ID und Secret in den User Secrets hinterlegen.");
            }

            var redirectUrl = $"/Account/ExternalLogin?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}";
            var properties = signInManager.ConfigureExternalAuthenticationProperties(
                DiscordAuthenticationDefaults.AuthenticationScheme, redirectUrl);
            return Results.Challenge(properties, [DiscordAuthenticationDefaults.AuthenticationScheme]);
        }).RequireRateLimiting(LoginRateLimitPolicy);

        // 2) Rückkanal nach erfolgreicher Discord-Authentifizierung.
        group.MapGet("/ExternalLogin", async (
            [FromServices] SignInManager<Agent> signInManager,
            [FromServices] UserManager<Agent> userManager,
            [FromServices] IConfiguration configuration,
            [FromServices] ILoggerFactory loggerFactory,
            [FromQuery] string? returnUrl,
            [FromQuery] string? remoteError) =>
        {
            var logger = loggerFactory.CreateLogger("NOOSE.ExternalLogin");
            returnUrl ??= "/";

            if (remoteError is not null)
            {
                return RedirectZurLoginSeite($"Discord meldete einen Fehler: {remoteError}");
            }

            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info is null)
            {
                return RedirectZurLoginSeite("Externe Anmeldedaten konnten nicht gelesen werden.");
            }

            var agent = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (agent is null)
            {
                agent = await ErstelleAgentAsync(userManager, info, configuration, logger);
                if (agent is null)
                {
                    return RedirectZurLoginSeite("Das NOOSE-Konto konnte nicht angelegt werden.");
                }
            }
            else
            {
                await AktualisiereStammdatenAsync(userManager, agent, info);
            }

            // Status-Gate: ausschließlich aktive Agenten erhalten eine Sitzung.
            switch (agent.Status)
            {
                case AgentStatus.Aktiv:
                    await signInManager.SignInAsync(agent, isPersistent: true);
                    return Results.LocalRedirect(returnUrl);
                case AgentStatus.Ausstehend:
                    return Results.Redirect("/Account/Ausstehend");
                default:
                    return Results.Redirect("/Account/Gesperrt");
            }
        }).RequireRateLimiting(LoginRateLimitPolicy);

        // 3) Abmelden.
        group.MapPost("/Logout", async (
            [FromServices] SignInManager<Agent> signInManager,
            [FromForm] string? returnUrl) =>
        {
            await signInManager.SignOutAsync();
            return Results.LocalRedirect(returnUrl ?? "/Account/Login");
        });

        return group;
    }

    private static IResult RedirectZurLoginSeite(string fehler)
        => Results.Redirect($"/Account/Login?fehler={Uri.EscapeDataString(fehler)}");

    private static async Task<Agent?> ErstelleAgentAsync(
        UserManager<Agent> userManager, ExternalLoginInfo info, IConfiguration configuration, ILogger logger)
    {
        var discordId = info.ProviderKey;
        var username = info.Principal.FindFirstValue(ClaimTypes.Name);
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);

        var bootstrapId = configuration["Bootstrap:AdminDiscordId"];
        var istBootstrapAdmin = !string.IsNullOrWhiteSpace(bootstrapId)
                                && string.Equals(bootstrapId.Trim(), discordId, StringComparison.Ordinal);

        var agent = new Agent
        {
            UserName = discordId, // Discord-Snowflake (nur Ziffern) – erfüllt die Identity-Namensregeln.
            Email = email,
            EmailConfirmed = email is not null,
            // Codename/Klarname/Dienstnummer bleiben leer – die Stammdaten vergibt ein Admin
            // auf der Agenten-Verwaltung. Der Discord-Name wird NIE als Codename übernommen.
            DiscordId = discordId,
            DiscordUsername = username,
            AvatarUrl = ExtrahiereAvatarUrl(info.Principal),
            RegistriertAm = DateTime.UtcNow,
            Status = istBootstrapAdmin ? AgentStatus.Aktiv : AgentStatus.Ausstehend,
            Dienstgrad = istBootstrapAdmin ? Dienstgrad.Director : null,
            IstAdmin = istBootstrapAdmin,
            FreigegebenAm = istBootstrapAdmin ? DateTime.UtcNow : null,
        };

        var create = await userManager.CreateAsync(agent);
        if (!create.Succeeded)
        {
            logger.LogError("Agent anlegen fehlgeschlagen ({DiscordId}): {Fehler}",
                discordId, string.Join("; ", create.Errors.Select(e => e.Description)));
            return null;
        }

        var addLogin = await userManager.AddLoginAsync(agent, info);
        if (!addLogin.Succeeded)
        {
            logger.LogError("Discord-Login verknüpfen fehlgeschlagen ({DiscordId}): {Fehler}",
                discordId, string.Join("; ", addLogin.Errors.Select(e => e.Description)));
            await userManager.DeleteAsync(agent);
            return null;
        }

        // Hinweis: Admin-Rechte laufen über das IstAdmin-Flag/Claim, nicht über Identity-Rollen.
        logger.LogInformation("Neuer Agent registriert: {DiscordUsername} ({DiscordId}), Status {Status}.",
            agent.DiscordUsername ?? discordId, discordId, agent.Status);
        return agent;
    }

    /// <summary>
    /// Hält die internen Discord-Stammdaten (Username, Avatar) bei jedem Login aktuell.
    /// Der nutzersichtbare Codename wird NIE aus Discord befüllt – er wird ausschließlich vom Admin vergeben.
    /// </summary>
    private static async Task AktualisiereStammdatenAsync(UserManager<Agent> userManager, Agent agent, ExternalLoginInfo info)
    {
        var username = info.Principal.FindFirstValue(ClaimTypes.Name);
        var avatar = ExtrahiereAvatarUrl(info.Principal);

        var geaendert = false;
        if (username is not null && username != agent.DiscordUsername) { agent.DiscordUsername = username; geaendert = true; }
        if (avatar is not null && avatar != agent.AvatarUrl) { agent.AvatarUrl = avatar; geaendert = true; }

        if (geaendert)
        {
            await userManager.UpdateAsync(agent);
        }
    }

    private static string? ExtrahiereAvatarUrl(ClaimsPrincipal principal)
        => principal.FindFirstValue("urn:discord:avatar:url")
           ?? principal.FindFirstValue("urn:discord:avatar");
}
