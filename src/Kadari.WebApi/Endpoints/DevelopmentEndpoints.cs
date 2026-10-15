using MediatR;
using Kadari.Application.Features.Development.SeedDemoHousehold;
using Kadari.Domain.Access;
using Kadari.WebApi.Access;

namespace Kadari.WebApi.Endpoints;

/// <summary>
/// Demo helpers, mapped only when the host runs in Development (see Program.cs). Deliberately not part of
/// <see cref="KadariApiEndpoints"/>, so they never appear in the published OpenAPI contract or a production host.
/// </summary>
public static class DevelopmentEndpoints
{
    public static IEndpointRouteBuilder MapDevelopmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/dev").WithTags("Development");

        group.MapPost("/customers/{customerId:guid}/demo-household", async (
                Guid customerId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new SeedDemoHouseholdCommand(customerId), ct)))
            .RequirePermission(Permission.AssetCapture)
            .WithName("SeedDemoHousehold")
            .WithSummary("Development only: fill the caller's empty ledger with a sample household.");

        return app;
    }
}
