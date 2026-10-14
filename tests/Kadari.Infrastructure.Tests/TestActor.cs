using Microsoft.Extensions.DependencyInjection;
using Kadari.Application.Abstractions.Access;
using Kadari.Domain.Access;

namespace Kadari.Infrastructure.Tests;

/// <summary>
/// Declares who is making the call in an infrastructure test (PAS-002 §2).
/// </summary>
/// <remarks>
/// Since resource authorization moved into the pipeline, a slice test has to say which actor it runs
/// as. Most of these tests are about persistence and workflow rather than access control, so they run
/// as staff — <see cref="AsStaff"/> — which is bounded by permissions rather than ownership. Tests
/// that are specifically about access control use <see cref="AsCustomer"/> and assert the refusal.
/// </remarks>
public sealed class TestActor(Guid? accountId, params Role[] roles) : ICurrentUser
{
    public Guid? AccountId => accountId;

    public IReadOnlyCollection<Role> Roles => roles;
}

public static class TestActorRegistration
{
    /// <summary>Runs the slice as back-office staff: permission-bounded, not ownership-bounded.</summary>
    public static IServiceCollection AsStaff(this IServiceCollection services)
        => services.AddSingleton<ICurrentUser>(
            new TestActor(Guid.CreateVersion7(), Role.SystemAdministrator, Role.CustomerSuccess, Role.Valuer));

    /// <summary>Runs the slice as a specific self-service customer, so ownership is enforced.</summary>
    public static IServiceCollection AsCustomer(this IServiceCollection services, Guid accountId)
        => services.AddSingleton<ICurrentUser>(new TestActor(accountId, Role.Customer));
}
