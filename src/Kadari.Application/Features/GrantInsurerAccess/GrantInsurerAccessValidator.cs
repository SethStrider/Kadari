using FluentValidation;

namespace Kadari.Application.Features.GrantInsurerAccess;

internal sealed class GrantInsurerAccessValidator : AbstractValidator<GrantInsurerAccessCommand>
{
    public GrantInsurerAccessValidator()
    {
        RuleFor(x => x.UserAccountId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
    }
}
