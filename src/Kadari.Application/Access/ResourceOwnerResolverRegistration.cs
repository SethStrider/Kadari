using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application.Abstractions.Access;

namespace Kadari.Application.Access;

/// <summary>
/// Discovers and registers every <see cref="IResourceOwnerResolver{TRequest}"/> in the assembly
/// (PAS-002 §2), so a new scoped slice needs no DI edit — the same convention validators follow.
/// </summary>
internal static class ResourceOwnerResolverRegistration
{
    public static IServiceCollection AddResourceOwnerResolvers(this IServiceCollection services, Assembly assembly)
    {
        var resolvers = assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
            .SelectMany(
                type => type.GetInterfaces()
                    .Where(i => i.IsGenericType
                                && i.GetGenericTypeDefinition() == typeof(IResourceOwnerResolver<>)),
                (type, contract) => (Implementation: type, Contract: contract));

        foreach (var (implementation, contract) in resolvers)
        {
            services.AddScoped(contract, implementation);
        }

        return services;
    }
}
