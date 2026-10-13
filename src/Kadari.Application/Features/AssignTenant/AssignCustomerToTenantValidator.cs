using FluentValidation;

namespace Kadari.Application.Features.AssignTenant;

internal sealed class AssignCustomerToTenantValidator : AbstractValidator<AssignCustomerToTenantCommand>
{
    public AssignCustomerToTenantValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
    }
}
