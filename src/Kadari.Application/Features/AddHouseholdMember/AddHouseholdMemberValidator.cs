using FluentValidation;

namespace Kadari.Application.Features.AddHouseholdMember;

internal sealed class AddHouseholdMemberValidator : AbstractValidator<AddHouseholdMemberCommand>
{
    public AddHouseholdMemberValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.MemberCustomerId).NotEmpty();
        RuleFor(x => x.MemberCustomerId)
            .NotEqual(x => x.CustomerId)
            .WithMessage("The primary owner cannot be added as their own co-owner.");
    }
}
