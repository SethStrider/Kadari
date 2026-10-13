using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Kadari.WebApi.OpenApi;

/// <summary>
/// Declares the JWT bearer scheme on the document (PAS-002 §5).
/// </summary>
/// <remarks>
/// Without this the published contract cannot say that anything needs a token: Scalar shows no
/// <em>Authorize</em> button, and a generated client emits unauthenticated calls against every
/// protected endpoint. The per-operation requirement is attached by
/// <see cref="PermissionRequirementTransformer"/>, which reads the permission each endpoint declares.
/// </remarks>
public sealed class SecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    /// <summary>The component key both this transformer and the operation transformer reference.</summary>
    public const string SchemeId = "bearer";

    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description =
                "Access token from `POST /api/v1/auth/otp/verify` (or `POST /api/v1/auth/refresh`). "
                + "Send as `Authorization: Bearer {token}`. Operations below list the permission the "
                + "token's roles must grant.",
        };

        return Task.CompletedTask;
    }
}
