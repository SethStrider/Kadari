using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Kadari.WebApi.OpenApi;

/// <summary>
/// Stamps the OpenAPI document with Trustli's API metadata (API design standard, #3).
/// Auth scheme wiring is a placeholder here and is completed with Identity &amp; Auth (#7).
/// </summary>
public sealed class ApiInfoTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Trustli API",
            Version = "v1",
            Description = """
                Trustli Decision Intelligence API. List endpoints are paged; mutating calls take an
                `Idempotency-Key`.

                **Errors.** Every failure — thrown or framework-generated — is
                `application/problem+json` (RFC 9457). Branch on the `code` extension, not on the
                status or title: statuses are coarse (`conflict`, `domain-rule-violated` and
                `concurrency-conflict` all answer 409) and titles are prose we may reword. Each
                response also carries `traceId` and, when supplied, your `correlationId` — quote them
                in a support request. A 400 from validation adds `errors`, a field-keyed map.
                """,
        };

        return Task.CompletedTask;
    }
}
