using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gocgun.Mediator;

/// <summary>Registers <see cref="ISender"/>, handlers, and open behaviors.</summary>
public static class MediatorServiceCollectionExtensions
{
    /// <summary>Scans <paramref name="assembly"/> and validates that each request has one handler.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">Assembly that contains handlers and requests.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddMediator(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return services.AddMediator(validateHandlerRegistry: true, assembly);
    }

    /// <summary>
    /// Scans <paramref name="assemblies"/>. When <paramref name="validateHandlerRegistry"/> is true,
    /// every request in those assemblies must already have exactly one handler.
    /// Pass every assembly in one call, or validate after the last call.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="validateHandlerRegistry">Whether to check the handler count now.</param>
    /// <param name="assemblies">Assemblies to scan. Null entries are skipped.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddMediator(
        this IServiceCollection services,
        bool validateHandlerRegistry,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        services.TryAddScoped<ISender, Sender>();

        foreach (var assembly in assemblies)
        {
            if (assembly is null)
            {
                continue;
            }

            RegisterHandlers(services, assembly);
        }

        if (validateHandlerRegistry)
        {
            HandlerRegistryValidator.Validate(services, assemblies);
        }

        return services;
    }

    /// <summary>
    /// Registers an open generic <see cref="IPipelineBehavior{TRequest, TResponse}"/>.
    /// Call this in the order behaviors should run.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="behaviorType">An open generic type with two type arguments.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddOpenBehavior(this IServiceCollection services, Type behaviorType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(behaviorType);
        if (!behaviorType.IsGenericTypeDefinition || behaviorType.GetGenericArguments().Length != 2)
        {
            throw new ArgumentException("Expected an open generic behavior with two type arguments.", nameof(behaviorType));
        }

        services.AddScoped(typeof(IPipelineBehavior<,>), behaviorType);
        return services;
    }

    private static void RegisterHandlers(IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(candidate => candidate is { IsAbstract: false, IsInterface: false }))
        {
            foreach (var contract in type.GetInterfaces())
            {
                if (!contract.IsGenericType || contract.GetGenericTypeDefinition() != typeof(IRequestHandler<,>))
                {
                    continue;
                }

                services.AddScoped(contract, type);
            }
        }
    }
}
