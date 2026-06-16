using Microsoft.AspNetCore.Components.Server.Circuits;

namespace NOOSE_Website.Infrastructure.CurrentUser;

/// <summary>
/// Macht den scoped <see cref="IServiceProvider"/> des aktuellen Blazor-Circuits über einen
/// <see cref="AsyncLocal{T}"/> zugänglich, damit app-weite (Singleton-)Dienste den circuit-gebundenen
/// <c>AuthenticationStateProvider</c> auflösen können. Notwendig, weil die DbContext-Factory jetzt
/// Singleton ist (Contexts nicht mehr circuit-gebunden, sonst „ObjectDisposedException: IServiceProvider").
/// Der eingeloggte Nutzer (für Audit/Read-Only-Sperre in den SaveChanges-Interceptors) wird im Circuit
/// aber nur über den per-Circuit scoped <c>AuthenticationStateProvider</c> aufgelöst — diesen liefert
/// der Accessor. Offizielles Muster: „Access server-side Blazor services from a different DI scope".
/// </summary>
public sealed class CircuitServicesAccessor
{
    private static readonly AsyncLocal<IServiceProvider?> Current = new();

    public IServiceProvider? Services
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}

/// <summary>
/// Setzt für die Dauer jeder eingehenden Circuit-Aktivität (UI-Event, JS-Interop) den scoped
/// IServiceProvider des Circuits in den <see cref="CircuitServicesAccessor"/> — der AsyncLocal-Wert
/// fließt von dort in die awaiteten Service-/SaveChanges-Aufrufe.
/// </summary>
internal sealed class CircuitServicesAccessorHandler(IServiceProvider services, CircuitServicesAccessor accessor)
    : CircuitHandler
{
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next)
        => async context =>
        {
            accessor.Services = services;
            try
            {
                await next(context);
            }
            finally
            {
                accessor.Services = null;
            }
        };
}

public static class CircuitServicesAccessorExtensions
{
    public static IServiceCollection AddCircuitServicesAccessor(this IServiceCollection services)
    {
        // Singleton (AsyncLocal-gestützt) — darf so in den Singleton-CurrentUserService injiziert werden.
        services.AddSingleton<CircuitServicesAccessor>();
        services.AddScoped<CircuitHandler, CircuitServicesAccessorHandler>();
        return services;
    }
}
