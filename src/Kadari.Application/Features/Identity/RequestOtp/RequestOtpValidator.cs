using FluentValidation;
using Kadari.Domain.Identity;

namespace Kadari.Application.Features.Identity.RequestOtp;

internal sealed class RequestOtpValidator : AbstractValidator<RequestOtpCommand>
{
    public RequestOtpValidator()
    {
        RuleFor(x => x.MobileNumber)
            .NotEmpty()
            .Must(m => MobileNumbers.Normalise(m) is not null).WithMessage("Enter a valid mobile number.");
    }
}
