using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Kadari.WebApi.Access;

namespace Kadari.WebApi.OpenApi;

/// <summary>
/// Projects each endpoint's <see cref="RequiredPermissionMetadata"/> into the OpenAPI document
/// (PAS-002 §5): a bearer security requirement, plus the permission name in the description.
/// </summary>
/// <remarks>
/// Authorization here is an <see cref="IEndpointFilter"/>, which contributes nothing to the generated
/// document on its own. That left the contract unable to express the single most important fact for an
/// integrator — which calls need a token, and what the token must be allowed to do. The filter now
/// also stamps metadata; this transformer reads it.
/// </remarks>
public sealed class PermissionRequirementTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        var required = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<RequiredPermissionMetadata>()
            .FirstOrDefault();

        if (required is null)
        {
            return Task.CompletedTask;   // deliberately anonymous — auth, webhooks, public verification
        }

        // The reference must be bound to the host document, otherwise it serialises as an empty
        // requirement object — present in the JSON, but naming no scheme and so telling a client
        // nothing.
        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SecuritySchemeTransformer.SchemeId, context.Document)] = [],
        });

        operation.Description = string.IsNullOrWhiteSpace(operation.Description)
            ? $"**Requires permission:** `{required.Permission}`"
            : $"{operation.Description}\n\n**Requires permission:** `{required.Permission}`";

        return Task.CompletedTask;
    }
}
