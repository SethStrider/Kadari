using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Exceptions;
using Kadari.Domain.Access;

namespace Kadari.Application.Behaviors;

/// <summary>
/// Enforces resource ownership for requests marked <see cref="IResourceScopedRequest"/> (PAS-002 §2).
/// The second half of authorization: the endpoint filter asks whether the caller's <em>role</em>
/// permits the action, this asks whether the <em>resource</em> is theirs.
/// </summary>
/// <remarks>
/// <para>
/// Lives in the pipeline rather than in handlers on purpose (CLAUDE.md §4). The three handlers that
/// checked ownership by hand proved the check was understood; what was missing was a mechanism that
/// applied it without being remembered.
/// </para>
/// <para>
/// Runs after validation (so a malformed id is rejected as a 400 before any lookup) and before the
/// transactional behaviour (so a refused request never opens a transaction).
/// </para>
/// </remarks>
internal sealed class ResourceAuthorizationBehavior<TRequest, TResponse>(
    IServiceProvider services,
    ICurrentUser currentUser,
    ICurrentCustomer currentCustomer)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IResourceScopedRequest scoped)
        {
            return await next(cancellationToken);
        }

        // Staff are bounded by their (narrow) permission set and must act across customers to work.
        if (!AccessPolicies.RequiresOwnershipScope(currentUser.Roles))
        {
            return await next(cancellationToken);
        }

        var owner = await ResolveOwnerAsync(request, cancellationToken);

        // A missing resource answers 404, never 403: a 403 on an id that does not exist confirms which
        // ids do, turning the endpoint into an enumeration oracle.
        if (owner is null)
        {
            throw new NotFoundException(typeof(TRequest).Name, scoped.ResourceId);
        }

        var callerId = owner.Value.Kind switch
        {
            ResourceOwnerKind.Customer => await currentCustomer.GetCustomerIdAsync(cancellationToken),
            ResourceOwnerKind.Account => currentUser.AccountId,
            _ => null,
        };

        if (!AccessPolicies.OwnsResource(callerId, owner.Value.OwnerId))
        {
            throw new ForbiddenException(
                $"Caller does not own resource {scoped.ResourceId} ({typeof(TRequest).Name}).");
        }

        return await next(cancellationToken);
    }

    /// <summary>
    /// Invokes the slice's <c>IResourceOwnerResolver&lt;TRequest&gt;</c>. Resolved through the container
    /// by the request's own type, so a scoped request with no resolver fails loudly at the first call
    /// rather than quietly skipping the check.
    /// </summary>
    /// <remarks>
    /// Reflection is needed because the behaviour is open-generic over <em>every</em> request, so
    /// <c>TRequest</c> cannot be statically constrained to <see cref="IResourceScopedRequest"/>. The
    /// runtime type is known to satisfy it — the caller has already pattern-matched on the marker.
    /// </remarks>
    private async Task<ResourceOwner?> ResolveOwnerAsync(TRequest request, CancellationToken cancellationToken)
    {
        var resolverType = typeof(IResourceOwnerResolver<>).MakeGenericType(typeof(TRequest));

        var resolver = services.GetService(resolverType)
            ?? throw new InvalidOperationException(
                $"{typeof(TRequest).Name} is marked {nameof(IResourceScopedRequest)} but no "
                + $"IResourceOwnerResolver<{typeof(TRequest).Name}> is registered for it.");

        var resolve = resolverType.GetMethod(
            nameof(IResourceOwnerResolver<IResourceScopedRequest>.ResolveAsync))!;

        return await (Task<ResourceOwner?>)resolve.Invoke(resolver, [request, cancellationToken])!;
    }
}
