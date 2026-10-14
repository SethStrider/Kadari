using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.DataRights;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.Customers.ExportCustomerData;

/// <summary>
/// POPIA subject-access request: assemble everything KADARI holds about a customer into one structured export, their
/// profile plus their assets, passports and consents (AS-03). A read aggregation. Admin-mediated and gated by <c>CustomerExport</c>, so it is <see cref="IUnscopedRequest"/>
/// (self-service SAR — a customer exporting their own data — is a resource-scoped follow-up).
/// </summary>
public sealed record ExportCustomerDataQuery(Guid CustomerId) : IQuery<CustomerDataExportDto>, IUnscopedRequest;

public sealed record CustomerProfileExport(
    Guid CustomerId, string FirstName, string LastName, string MobileNumber, string Country,
    string? Email, string? IdentityNumber, DateOnly? DateOfBirth, string Status);

public sealed record CustomerDataExportDto(CustomerProfileExport Profile, KadariDataExportDto Ledger);

internal sealed class ExportCustomerDataHandler(IKadariDbContext db, KadariDataExport kadari)
    : IQueryHandler<ExportCustomerDataQuery, CustomerDataExportDto>
{
    public async Task<CustomerDataExportDto> Handle(ExportCustomerDataQuery query, CancellationToken ct)
    {
        var customerId = new CustomerId(query.CustomerId);

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct)
            ?? throw new NotFoundException(nameof(Customer), query.CustomerId);

        var profile = new CustomerProfileExport(
            customer.Id.Value, customer.FirstName, customer.LastName, customer.MobileNumber, customer.Country,
            customer.Email, customer.IdentityNumber, customer.DateOfBirth, customer.Status.ToString());

        var kadariData = await kadari.ExportAsync(customerId, ct);

        return new CustomerDataExportDto(profile, kadariData);
    }
}
