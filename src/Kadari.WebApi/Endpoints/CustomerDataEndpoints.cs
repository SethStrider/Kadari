using MediatR;
using Kadari.Application.Features.Customers.EraseCustomer;
using Kadari.Application.Features.Customers.ExportCustomerData;
using Kadari.Application.Features.Customers.GetCurrentCustomer;
using Kadari.Domain.Access;
using Kadari.WebApi.Access;

namespace Kadari.WebApi.Endpoints;

/// <summary>The signed-in customer's own record, and the POPIA data-lifecycle endpoints: erase (right to be
/// forgotten) and export (subject-access request).</summary>
public static class CustomerDataEndpoints
{
    public static IEndpointRouteBuilder MapCustomerDataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/customers").WithTags("Customers");

        // The caller's own customer id + basic profile, resolved from the token: how the client turns a sign-in into
        // the customer id its scoped calls need. Authenticated, no special permission.
        group.MapGet("/me", async (ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetCurrentCustomerQuery(), ct)))
            .RequireAuthorization()
            .WithName("GetCurrentCustomer")
            .WithSummary("The caller's own customer id and basic profile (resolved from the token).")
            .Produces<CurrentCustomerResponse>();

        group.MapPost("/{customerId:guid}/erase", async (Guid customerId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new EraseCustomerCommand(customerId), ct)))
            .RequirePermission(Permission.CustomerErase)
            .WithName("EraseCustomer")
            .WithSummary("Erase a customer's personal data and ledger under POPIA (anonymised in place; audit preserved).")
            .Produces<EraseCustomerResponse>();

        group.MapGet("/{customerId:guid}/export", async (Guid customerId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ExportCustomerDataQuery(customerId), ct)))
            .RequirePermission(Permission.CustomerExport)
            .WithName("ExportCustomerData")
            .WithSummary("Export everything KADARI holds about a customer (POPIA subject-access request).")
            .Produces<CustomerDataExportDto>();

        return group;
    }
}
