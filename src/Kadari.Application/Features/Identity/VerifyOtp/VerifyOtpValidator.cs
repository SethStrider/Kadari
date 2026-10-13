using FluentValidation;

namespace Kadari.Application.Features.Identity.VerifyOtp;

internal sealed class VerifyOtpValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpValidator()
    {
        RuleFor(x => x.MobileNumber).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().Length(4, 8);
    }
}
