using System.Reflection;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;

namespace Kadari.ArchitectureTests;

/// <summary>
/// The fail-closed lock on resource authorization (PAS-002 §2.5).
/// </summary>
/// <remarks>
/// <para>
/// Authorization used to be role-only, and the three handlers that checked ownership did so by hand.
/// The gap was not that anyone disagreed with the check — it was that nothing made its absence
/// visible. These tests make it visible: a slice that declares neither marker, or declares itself
/// scoped without supplying a resolver, fails the build.
/// </para>
/// <para>
/// If one of these fails on a slice you just added, the fix is to decide which the request is and say
/// so — not to relax the test.
/// </para>
/// </remarks>
public class ResourceAuthorizationRuleTests
{
    private static readonly Assembly Application = typeof(Kadari.Application.DependencyInjection).Assembly;

    /// <summary>Every concrete command/query defined under <c>Features/</c>.</summary>
    private static IEnumerable<Type> FeatureRequests() =>
        Application.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
            .Where(t => t.Namespace?.StartsWith("Kadari.Application.Features", StringComparison.Ordinal) == true)
            .Where(t => t.GetInterfaces().Any(IsRequestContract));

    private static bool IsRequestContract(Type i)
    {
        if (i == typeof(ICommand))
        {
            return true;
        }

        if (!i.IsGenericType)
        {
            return false;
        }

        var definition = i.GetGenericTypeDefinition();
        return definition == typeof(ICommand<>) || definition == typeof(IQuery<>);
    }

    [Fact]
    public void Every_feature_request_declares_its_authorization_scope()
    {
        var undeclared = FeatureRequests()
            .Where(t => !typeof(IResourceScopedRequest).IsAssignableFrom(t)
                        && !typeof(IUnscopedRequest).IsAssignableFrom(t))
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToArray();

        Assert.True(
            undeclared.Length == 0,
            "These requests declare neither IResourceScopedRequest nor IUnscopedRequest, so the "
            + "authorization pipeline would skip them silently. Mark each one (PAS-002 §2):\n  "
            + string.Join("\n  ", undeclared));
    }

    [Fact]
    public void No_request_declares_both_scopes()
    {
        var both = FeatureRequests()
            .Where(t => typeof(IResourceScopedRequest).IsAssignableFrom(t)
                        && typeof(IUnscopedRequest).IsAssignableFrom(t))
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToArray();

        Assert.True(both.Length == 0, $"Contradictory scope markers on: {string.Join(", ", both)}");
    }

    [Fact]
    public void Every_scoped_request_has_an_owner_resolver()
    {
        var resolverTargets = Application.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IResourceOwnerResolver<>))
            .Select(i => i.GetGenericArguments()[0])
            .ToHashSet();

        var missing = FeatureRequests()
            .Where(typeof(IResourceScopedRequest).IsAssignableFrom)
            .Where(t => !resolverTargets.Contains(t))
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "These requests are resource-scoped but have no IResourceOwnerResolver, so the behaviour "
            + "throws at runtime instead of authorizing. Add one in the slice folder:\n  "
            + string.Join("\n  ", missing));
    }

    [Fact]
    public void Detector_finds_the_requests_it_is_meant_to_guard()
    {
        // Guards against the suite passing because the reflection query silently matched nothing.
        Assert.True(FeatureRequests().Count() > 25);
        Assert.Contains(FeatureRequests(), t => typeof(IResourceScopedRequest).IsAssignableFrom(t));
        Assert.Contains(FeatureRequests(), t => typeof(IUnscopedRequest).IsAssignableFrom(t));
    }
}
