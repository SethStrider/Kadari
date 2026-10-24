using FluentValidation;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.CompleteRegistration;

internal sealed class CompleteRegistrationValidator : AbstractValidator<CompleteRegistrationCommand>
{
    public CompleteRegistrationValidator(TimeProvider clock)
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.RegistrationId).NotEmpty();
        RuleFor(x => x.ExternalReference).NotEmpty().MaximumLength(CoverageRegistration.MaxReferenceLength)
            .WithMessage("Add the confirmation or reference number the provider gave you.");
        RuleFor(x => x.RegisteredOn)
            .Must(d => d <= DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
            .WithMessage("The registration date cannot be in the future.");
    }
}
