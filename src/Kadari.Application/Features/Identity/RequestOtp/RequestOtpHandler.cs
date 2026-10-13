using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Kadari.Application.Abstractions.Identity;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Identity.Services;
using Kadari.Domain.Identity;

namespace Kadari.Application.Features.Identity.RequestOtp;

internal sealed class RequestOtpHandler(
    IKadariDbContext db,
    IOtpCodeGenerator codes,
    IOtpDelivery delivery,
    IPostCommitActions postCommit,
    IOptions<OtpOptions> options,
    TimeProvider clock)
    : ICommandHandler<RequestOtpCommand, RequestOtpResponse>
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MinResendInterval = TimeSpan.FromSeconds(60);

    // Per-number budgets (GL-15). The per-IP rate limit cannot see a distributed attacker; these can. With five
    // attempts per code, ten codes a day caps guessing at fifty tries per number per day, and bounds what an
    // SMS-pumping run can spend on one number.
    private const int MaxCodesPerHour = 5;
    private const int MaxCodesPerDay = 10;

    public async Task<RequestOtpResponse> Handle(RequestOtpCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        // One spelling per phone (AS-10), so 082..., 27... and +27... share one budget and one account, and only
        // the allowed countries receive a paid code.
        var mobile = MobileNumbers.Normalise(command.MobileNumber)
            ?? throw Invalid("Enter a valid mobile number.");
        var countries = options.Value.CountryCodes;
        if (countries.Count > 0 && !MobileNumbers.IsInCountry(mobile, countries))
        {
            throw Invalid("Sign-in codes can only be sent to South African mobile numbers.");
        }

        // Rate limit: a still-fresh pending challenge blocks a resend (PCS-004 §12).
        var latestPending = await db.Set<OtpChallenge>()
            .Where(c => c.MobileNumber == mobile && c.Status == OtpStatus.Pending)
            .OrderByDescending(c => c.ExpiresAt)
            .FirstOrDefaultAsync(ct);

        if (latestPending is not null && latestPending.ExpiresAt - Ttl + MinResendInterval > now)
        {
            throw new ConflictException("An OTP was requested recently. Please wait before requesting another.");
        }

        // A challenge's issue time is its expiry minus the TTL, so "issued since T" is "expires after T + TTL".
        var issued = db.Set<OtpChallenge>().Where(c => c.MobileNumber == mobile);
        var lastHour = await issued.CountAsync(c => c.ExpiresAt > now.Add(Ttl).AddHours(-1), ct);
        var lastDay = await issued.CountAsync(c => c.ExpiresAt > now.Add(Ttl).AddDays(-1), ct);
        if (lastHour >= MaxCodesPerHour || lastDay >= MaxCodesPerDay)
        {
            throw new TooManyRequestsException($"OTP budget spent for this number ({lastHour}/h, {lastDay}/day).");
        }

        var code = codes.Generate();
        var challenge = OtpChallenge.Issue(mobile, codes.Hash(code), now.Add(Ttl));

        db.Set<OtpChallenge>().Add(challenge);
        await db.SaveChangesAsync(ct);

        // Sent after the commit, not inside it (AS-23): a transient database error re-runs this handler, and an
        // in-transaction send would text a second, different code. If the process dies before the send, the
        // user asks again; the code never goes into the outbox or the message broker.
        postCommit.Enqueue(sendCt => delivery.SendAsync(mobile, code, sendCt));

        return new RequestOtpResponse(challenge.Id.Value, challenge.ExpiresAt);
    }

    private static ValidationException Invalid(string message) =>
        new(new Dictionary<string, string[]> { [nameof(RequestOtpCommand.MobileNumber)] = [message] });
}
