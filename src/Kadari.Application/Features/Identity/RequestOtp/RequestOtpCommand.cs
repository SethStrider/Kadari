using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;

namespace Kadari.Application.Features.Identity.RequestOtp;

public sealed record RequestOtpCommand(string MobileNumber) : ICommand<RequestOtpResponse>, IUnscopedRequest;

public sealed record RequestOtpResponse(Guid ChallengeId, DateTimeOffset ExpiresAt);
