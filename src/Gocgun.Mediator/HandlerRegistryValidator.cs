using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Gocgun.Mediator;

/// <summary>
/// Checks that every <see cref="IRequest{TResponse}"/> in the scanned assemblies has exactly one handler.
/// A request whose handler was registered from an assembly that was not passed in fails this check.
/// </summary>
public static class HandlerRegistryValidator
{
    /// <summary>Throws when a scanned request does not have exactly one handler.</summary>
    /// <param name="services">Registrations produced by <c>AddMediator</c>.</param>
    /// <param name="assemblies">Assemblies whose request types must be covered.</param>
    public static void Validate(IServiceCollection services, IReadOnlyList<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            if (assembly is null)
            {
                continue;
            }

            foreach (var requestType in assembly.GetTypes().Where(IsConcreteRequest))
            {
                var responseType = requestType
                    .GetInterfaces()
                    .First(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IRequest<>))
                    .GetGenericArguments()[0];
                var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
                var count = services.Count(descriptor => descriptor.ServiceType == handlerType);
                if (count != 1)
                {
                    throw new InvalidOperationException(
                        $"Expected exactly one handler for {requestType.FullName}; found {count}.");
                }
            }
        }
    }

    private static bool IsConcreteRequest(Type type) =>
        type is { IsAbstract: false, IsInterface: false }
        && type.GetInterfaces().Any(contract =>
            contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IRequest<>));
}
