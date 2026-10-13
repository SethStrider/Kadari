using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;

namespace Kadari.WebApi.Http;

/// <summary>
/// Request body and time budgets (GL-18). Before this every endpoint accepted Kestrel's 30 MB default and could run
/// forever, so a handful of slow or oversized requests could pin memory and threads.
/// </summary>
/// <remarks>
/// JSON endpoints get <see cref="DefaultMaxBodyBytes"/> and <see cref="DefaultTimeout"/>. The few endpoints that take
/// a file or do slow work opt in explicitly with <see cref="AllowUploads{TBuilder}"/> or
/// <see cref="AllowLongRunning{TBuilder}"/>, so a new endpoint is small and quick unless someone decides otherwise.
/// </remarks>
public static class RequestLimits
{
    public const long DefaultMaxBodyBytes = 1024 * 1024;

    /// <summary>A 10 MB file plus multipart framing.</summary>
    public const long UploadMaxBodyBytes = 12 * 1024 * 1024;

    public const string LongRunningPolicy = "long-running";

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>A vision model call (up to 90 s) or a report render, with headroom.</summary>
    public static readonly TimeSpan LongRunningTimeout = TimeSpan.FromSeconds(120);

    public static WebApplicationBuilder AddKadariRequestLimits(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = DefaultMaxBodyBytes);
        builder.Services.Configure<FormOptions>(form => form.MultipartBodyLengthLimit = UploadMaxBodyBytes);
        builder.Services.AddRequestTimeouts(timeouts =>
        {
            timeouts.DefaultPolicy = new RequestTimeoutPolicy { Timeout = DefaultTimeout };
            timeouts.AddPolicy(LongRunningPolicy, LongRunningTimeout);
        });

        return builder;
    }

    /// <summary>Accepts a file upload (up to <see cref="UploadMaxBodyBytes"/>) with the long-running time budget.</summary>
    public static TBuilder AllowUploads<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new RequestSizeLimitAttribute(UploadMaxBodyBytes));
        return builder.AllowLongRunning();
    }

    public static TBuilder AllowLongRunning<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithRequestTimeout(LongRunningPolicy);
        return builder;
    }
}
