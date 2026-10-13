using FluentValidation;

namespace Kadari.Application.Features.Identity.RefreshToken;

internal sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}
