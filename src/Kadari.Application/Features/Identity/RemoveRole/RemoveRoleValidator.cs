using FluentValidation;

namespace Kadari.Application.Features.Identity.RemoveRole;

internal sealed class RemoveRoleValidator : AbstractValidator<RemoveRoleCommand>
{
    public RemoveRoleValidator()
    {
        RuleFor(x => x.UserAccountId).NotEmpty();
        RuleFor(x => x.Role).IsInEnum();
    }
}
