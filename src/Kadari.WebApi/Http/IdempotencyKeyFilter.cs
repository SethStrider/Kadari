using Kadari.Application.Exceptions;

namespace Kadari.WebApi.Http;

/// <summary>
/// Enforces the idempotency-key convention (API design standard, #3) on mutating endpoints:
/// the caller must supply an <c>Idempotency-Key</c> header so a retried request can be recognised.
/// <para>
/// This filter validates the header's presence only. The persistent dedupe store (remembering keys
/// and short-circuiting replays) arrives with Payments (#11) and needs the database — until then
/// the convention is enforced at the edge but not yet persisted.
/// </para>
/// </summary>
public sealed class IdempotencyKeyFilter : IEndpointFilter
{
    public const string HeaderName = "Idempotency-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var header = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(header))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [HeaderName] = ["An Idempotency-Key header is required for this request."],
            });
        }

        return await next(context);
    }
}
