using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Kadari.WebApi.Endpoints;

namespace Kadari.WebApi.OpenApi;

/// <summary>
/// Writes the OpenAPI document to a file without standing up the real application (PAS-002 §6).
/// Invoked as <c>dotnet run --project src/Kadari.WebApi -- --emit-openapi &lt;path&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// The committed <c>openapi/v1.json</c> is what the TypeScript client is generated from, so it has to
/// be producible in CI — but the full composition root needs Postgres, RabbitMQ, S3 and a validated
/// secret set before it will start. Build-time emission via
/// <c>Microsoft.Extensions.ApiDescription.Server</c> boots that same root, so it fails for the same
/// reason. Relaxing options validation to make it pass would trade a real safeguard for a build
/// convenience.
/// </para>
/// <para>
/// Instead this composes only what a document needs: the OpenAPI services, the transformers, and the
/// same <c>MapKadariApiEndpoints()</c> the real host uses. Endpoint delegates are never invoked during
/// generation, so their dependencies need not work — only be registered (see
/// <see cref="RegisterServiceParameterMarkers"/>).
/// </para>
/// </remarks>
public static class OpenApiDocumentEmitter
{
    public const string Switch = "--emit-openapi";

    public static async Task<int> EmitAsync(string[] args)
    {
        var outputPath = Path.GetFullPath(
            args.SkipWhile(a => a != Switch).Skip(1).FirstOrDefault() ?? "openapi/v1.json");

        var port = FreePort();

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

        builder.Services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer<ApiInfoTransformer>();
            options.AddDocumentTransformer<SecuritySchemeTransformer>();
            options.AddOperationTransformer<ProblemResponsesTransformer>();
            options.AddOperationTransformer<PermissionRequirementTransformer>();
        });

        RegisterServiceParameterMarkers(builder.Services);

        var app = builder.Build();
        app.MapOpenApi();
        app.MapKadariApiEndpoints();

        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            using var response = await client.GetAsync("/openapi/v1.json");

            var document = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // Surface the generator's own complaint; a bare status tells the operator nothing.
                await Console.Error.WriteLineAsync(
                    $"OpenAPI generation failed ({(int)response.StatusCode}):{Environment.NewLine}{document}");
                return 1;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            // Newline-terminated so the file is diff-clean and CI's "did this change" check is honest.
            await File.WriteAllTextAsync(outputPath, document.TrimEnd() + Environment.NewLine);

            Console.WriteLine($"Wrote OpenAPI document to {outputPath}");
            return 0;
        }
        finally
        {
            await app.StopAsync();
        }
    }

    /// <summary>
    /// Registers the service types endpoint delegates take as parameters.
    /// </summary>
    /// <remarks>
    /// Minimal APIs decide whether a parameter comes from DI or from the request by asking the
    /// container whether the type is registered. Unregistered service types are treated as body/route
    /// parameters and generation fails with "failure to infer one or more parameters". Nothing here is
    /// ever resolved — no delegate runs during generation — so each factory throws rather than
    /// pretending to be a working implementation.
    /// </remarks>
    private static void RegisterServiceParameterMarkers(IServiceCollection services)
    {
        Marker<MediatR.ISender>();
        Marker<Application.Abstractions.Access.ICurrentUser>();
        Marker<Application.Abstractions.Storage.IBlobStorage>();
        Marker<Application.Abstractions.Reports.IReportMediaLinkFactory>();

        void Marker<T>() where T : class
            => services.AddTransient<T>(_ => throw new NotSupportedException(
                $"{typeof(T).Name} is a document-generation placeholder and must never be resolved."));
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
