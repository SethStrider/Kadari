using FluentValidation;
using Kadari.Domain.Access;

namespace Kadari.Application.Features.Identity.AssignRole;

internal sealed class AssignRoleValidator : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleValidator()
    {
        RuleFor(x => x.UserAccountId).NotEmpty();

        RuleFor(x => x.Role)
            .IsInEnum()
            // PublicVisitor is what an *unauthenticated* caller is, not something an account holds.
            // Granting it would be meaningless and would muddy the "every account has a real role" rule.
            .NotEqual(Role.PublicVisitor)
            .WithMessage("PublicVisitor is the anonymous role and cannot be granted to an account.")
            // An Insurer is only meaningful bound to its tenant; granting the bare role here would leave an
            // insurer with no binding (AS-04). GrantInsurerAccess adds the role and the binding together.
            .NotEqual(Role.Insurer)
            .WithMessage("Grant insurer access through the insurer tenant, which binds the account to it.");
    }
}
