using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;

namespace Kadari.Application.Features.Identity.VerifyOtp;

public sealed record VerifyOtpCommand(string MobileNumber, string Code) : ICommand<VerifyOtpResponse>, IUnscopedRequest;

public sealed record VerifyOtpResponse(
    string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken, Guid UserAccountId);
