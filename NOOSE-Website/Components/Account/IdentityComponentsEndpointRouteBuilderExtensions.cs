using System.Security.Claims;
using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Components.Account;

/// <summary>
/// Statische HTTP-Endpoints fuer den Login-/Logout-Fluss. Diese koennen NICHT in interaktiven
/// Komponenten leben, weil der OAuth-Ablauf echte HTTP-Redirects (zu Discord und zurueck) braucht.
/// </summary>
public static class IdentityComponentsEndpointRouteBuilderExtensions
{
    /// <summary>Name der Rate-Limiting-Policy fuer den Login-Start (in Program.cs konfiguriert).</summary>
    public const string LoginRateLimitPolicy = "noose-login";

    public static IEndpointConventionBuilder MapNooseAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/Account");

        // 1) Login-Start: fordert den Discord-OAuth-Flow an.
        group.MapPost("/PerformExternalLogin", (
            HttpContext context,
            [FromServices] SignInManager<Agent> signInManager,
            [FromForm] string? returnUrl) =>
        {
            var redirectUrl = $"/Account/ExternalLogin?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}";
            var properties = signInManager.ConfigureExternalAuthenticationProperties(
                DiscordAuthenticationDefaults.AuthenticationScheme, redirectUrl);
            return Results.Challenge(properties, [DiscordAuthenticationDefaults.AuthenticationScheme]);
        }).RequireRateLimiting(LoginRateLimitPolicy);

        // 2) Rueckkanal nach erfolgreicher Discord-Authentifizierung.
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

            // Status-Gate: ausschliesslich aktive Agenten erhalten eine Sitzung.
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
        });

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
        var globalName = info.Principal.FindFirstValue("urn:discord:global_name");
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);

        var bootstrapId = configuration["Bootstrap:AdminDiscordId"];
        var istBootstrapAdmin = !string.IsNullOrWhiteSpace(bootstrapId)
                                && string.Equals(bootstrapId.Trim(), discordId, StringComparison.Ordinal);

        var agent = new Agent
        {
            UserName = discordId, // Discord-Snowflake (nur Ziffern) – erfuellt die Identity-Namensregeln.
            Email = email,
            EmailConfirmed = email is not null,
            Anzeigename = FirstNonEmpty(globalName, username) ?? $"Agent {discordId}",
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
            logger.LogError("Discord-Login verknuepfen fehlgeschlagen ({DiscordId}): {Fehler}",
                discordId, string.Join("; ", addLogin.Errors.Select(e => e.Description)));
            await userManager.DeleteAsync(agent);
            return null;
        }

        // Hinweis: Admin-Rechte laufen ueber das IstAdmin-Flag/Claim, nicht ueber Identity-Rollen.
        logger.LogInformation("Neuer Agent registriert: {Anzeigename} ({DiscordId}), Status {Status}.",
            agent.Anzeigename, discordId, agent.Status);
        return agent;
    }

    /// <summary>Haelt Anzeigename/Username/Avatar bei jedem Login mit Discord aktuell.</summary>
    private static async Task AktualisiereStammdatenAsync(UserManager<Agent> userManager, Agent agent, ExternalLoginInfo info)
    {
        var username = info.Principal.FindFirstValue(ClaimTypes.Name);
        var globalName = info.Principal.FindFirstValue("urn:discord:global_name");
        var avatar = ExtrahiereAvatarUrl(info.Principal);

        var geaendert = false;
        if (username is not null && username != agent.DiscordUsername) { agent.DiscordUsername = username; geaendert = true; }
        if (avatar is not null && avatar != agent.AvatarUrl) { agent.AvatarUrl = avatar; geaendert = true; }

        var neuerName = FirstNonEmpty(globalName, username);
        if (neuerName is not null && string.IsNullOrWhiteSpace(agent.Anzeigename)) { agent.Anzeigename = neuerName; geaendert = true; }

        if (geaendert)
        {
            await userManager.UpdateAsync(agent);
        }
    }

    private static string? ExtrahiereAvatarUrl(ClaimsPrincipal principal)
        => principal.FindFirstValue("urn:discord:avatar:url")
           ?? principal.FindFirstValue("urn:discord:avatar");

    private static string? FirstNonEmpty(params string?[] werte)
        => werte.FirstOrDefault(w => !string.IsNullOrWhiteSpace(w));
}
