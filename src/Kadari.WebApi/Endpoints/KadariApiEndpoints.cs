namespace Kadari.WebApi.Endpoints;

/// <summary>Every feature endpoint group, in one place so the host and the OpenAPI emitter map the same set.</summary>
public static class KadariApiEndpoints
{
    public static IEndpointRouteBuilder MapKadariApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAuthEndpoints();
        app.MapSessionEndpoints();
        app.MapAdminUserEndpoints();
        app.MapCustomerDataEndpoints();
        app.MapKadariEndpoints();
        app.MapCareEndpoints();

        return app;
    }
}
