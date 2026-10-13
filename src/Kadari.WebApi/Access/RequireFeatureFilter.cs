using Microsoft.Extensions.Options;
using Kadari.Application.Abstractions.Configuration;

namespace Kadari.WebApi.Access;

/// <summary>
/// Endpoint filter that answers 404 while a launch switch in <see cref="FeatureOptions"/> is off (GL-27/28).
/// </summary>
/// <remarks>
/// 404, not 403: a switched-off capability is absent, and saying "forbidden" would tell a caller it exists. Add
/// it before <c>RequirePermission</c> so an anonymous caller gets the same answer as a signed-in one. The routes
/// stay mapped so the published OpenAPI contract does not change with configuration.
/// </remarks>
public sealed class RequireFeatureFilter(Func<FeatureOptions, bool> isEnabled) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var features = context.HttpContext.RequestServices.GetRequiredService<IOptions<FeatureOptions>>().Value;

        return isEnabled(features) ? await next(context) : Results.NotFound();
    }
}

public static class RequireFeatureExtensions
{
    /// <summary>Serves the endpoint only while <paramref name="isEnabled"/> holds for the current switches.</summary>
    public static TBuilder RequireFeature<TBuilder>(this TBuilder builder, Func<FeatureOptions, bool> isEnabled)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(new RequireFeatureFilter(isEnabled));
        return builder;
    }
}
