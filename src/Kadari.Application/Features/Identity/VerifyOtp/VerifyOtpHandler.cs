using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Identity;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Identity.Services;
using Kadari.Domain.Identity;

namespace Kadari.Application.Features.Identity.VerifyOtp;

internal sealed class VerifyOtpHandler(
    IKadariDbContext db,
    IOtpCodeGenerator codes,
    ITokenService tokens,
    IRefreshTokenService refreshTokens,
    TimeProvider clock)
    : ICommandHandler<VerifyOtpCommand, VerifyOtpResponse>
{
    public async Task<VerifyOtpResponse> Handle(VerifyOtpCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        // The same spelling the request stored (AS-10); an unreadable number simply matches no challenge.
        var mobile = MobileNumbers.Normalise(command.MobileNumber) ?? command.MobileNumber.Trim();

        var challenge = await db.Set<OtpChallenge>()
            .Where(c => c.MobileNumber == mobile && c.Status == OtpStatus.Pending)
            .OrderByDescending(c => c.ExpiresAt)
            .FirstOrDefaultAsync(ct)
            // The key is logged with the exception, so it names no mobile number (GL-39).
            ?? throw new NotFoundException(nameof(OtpChallenge), "pending code");

        var outcome = challenge.Verify(codes.Hash(command.Code), now);
        await db.SaveChangesAsync(ct);   // persist the attempt/status regardless of outcome

        if (outcome != OtpVerification.Success)
        {
            // IPersistableRejection: the failed-attempt increment above must survive the rejection so
            // brute-force protection holds, even though the command's transaction is otherwise rolled back.
            throw new OtpRejectedException($"OTP verification failed ({outcome}).");
        }

        // Auto-register a Customer on first verify of an unknown mobile (WhatsApp-first).
        var user = await db.Set<UserAccount>().FirstOrDefaultAsync(u => u.MobileNumber == mobile, ct);
        if (user is null)
        {
            user = UserAccount.ProvisionForMobile(mobile);
            db.Set<UserAccount>().Add(user);
        }
        else if (!user.CanSignIn)
        {
            // Checked only after the code proved possession of the number, so the answer reveals the account's
            // state to its owner alone (GL-12).
            throw new ForbiddenException("This account is not active.");
        }

        user.RecordLogin(now);

        // Start a session with a refresh token (PCS-004 §8).
        var refreshToken = refreshTokens.Generate();
        var session = Session.Start(user.Id, refreshTokens.Hash(refreshToken), now, now.Add(SessionLifetime.Refresh));
        db.Set<Session>().Add(session);

        await db.SaveChangesAsync(ct);

        var token = tokens.Issue(user, session.Id);
        return new VerifyOtpResponse(token.Token, token.ExpiresAt, refreshToken, user.Id.Value);
    }
}
