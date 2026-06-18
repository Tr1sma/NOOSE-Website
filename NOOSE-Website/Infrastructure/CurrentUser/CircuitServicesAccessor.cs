using Microsoft.AspNetCore.Components.Server.Circuits;

namespace NOOSE_Website.Infrastructure.CurrentUser;

/// <summary>Exposes the current circuit's scoped <see cref="IServiceProvider"/> via <see cref="AsyncLocal{T}"/> so singleton services can resolve the circuit-scoped AuthenticationStateProvider.</summary>
public sealed class CircuitServicesAccessor
{
    private static readonly AsyncLocal<IServiceProvider?> Current = new();

    public IServiceProvider? Services
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}

/// <summary>Sets the circuit's scoped service provider into <see cref="CircuitServicesAccessor"/> for the duration of each inbound circuit activity.</summary>
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
        services.AddSingleton<CircuitServicesAccessor>();
        services.AddScoped<CircuitHandler, CircuitServicesAccessorHandler>();
        return services;
    }
}
