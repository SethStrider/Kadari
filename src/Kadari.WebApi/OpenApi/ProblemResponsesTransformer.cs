using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Kadari.WebApi.ExceptionHandling;

namespace Kadari.WebApi.OpenApi;

/// <summary>
/// Documents the shared error contract on every operation, so the failure modes are visible in Scalar
/// without each endpoint repeating <c>ProducesProblem</c> (CLAUDE.md §8 — extend by adding, not by
/// editing every call site).
/// <para>
/// An operation that declares its own response for a status keeps it; this only fills the gaps.
/// </para>
/// </summary>
public sealed class ProblemResponsesTransformer : IOpenApiOperationTransformer
{
    /// <summary>Any endpoint can reject the request, refuse the caller, or fault.</summary>
    private static readonly int[] Universal =
    [
        StatusCodes.Status400BadRequest,
        StatusCodes.Status401Unauthorized,
        StatusCodes.Status403Forbidden,
        StatusCodes.Status500InternalServerError,
    ];

    public async Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        var schema = await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), cancellationToken: ct);

        operation.Responses ??= [];

        foreach (var status in StatusesFor(context))
        {
            var key = status.ToString(CultureInfo.InvariantCulture);

            if (operation.Responses.ContainsKey(key))
            {
                continue;
            }

            var problem = ApiProblems.FromStatus(status);

            operation.Responses[key] = new OpenApiResponse
            {
                Description = $"{problem.Title} (code: `{problem.Code}`)",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new() { Schema = schema },
                },
            };
        }
    }

    private static IEnumerable<int> StatusesFor(OpenApiOperationTransformerContext context)
    {
        foreach (var status in Universal)
        {
            yield return status;
        }

        // Addresses a specific resource, so that resource can be missing.
        if (context.Description.ParameterDescriptions.Any(p => p.Source == BindingSource.Path))
        {
            yield return StatusCodes.Status404NotFound;
        }

        // Changes state, so it can collide with the current state or with a competing writer.
        if (!HttpMethods.IsGet(context.Description.HttpMethod ?? string.Empty)
            && !HttpMethods.IsHead(context.Description.HttpMethod ?? string.Empty))
        {
            yield return StatusCodes.Status409Conflict;
        }
    }
}
