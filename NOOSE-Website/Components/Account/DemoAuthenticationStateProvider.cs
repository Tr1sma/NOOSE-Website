using Microsoft.AspNetCore.Components.Authorization;
using NOOSE_Website.Infrastructure;

namespace NOOSE_Website.Components.Account;

/// <summary>Demo instance only: presents every request and circuit as the static read-only demo agent. A plain provider is not an IHostEnvironmentAuthenticationStateProvider, so the framework never seeds or revalidates it — the principal can't be flipped to anonymous mid-circuit (which would dead-end on the disabled login).</summary>
internal sealed class DemoAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly Task<AuthenticationState> DemoState =
        Task.FromResult(new AuthenticationState(DemoIdentity.BuildPrincipal()));

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => DemoState;
}
