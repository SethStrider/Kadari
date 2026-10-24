using Amazon.S3;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Kadari.Application.Abstractions.Identity;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Notifications;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Reports;
using Kadari.Application.Abstractions.Storage;
using Kadari.Infrastructure.Http;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Notifications;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;
using Kadari.Infrastructure.Persistence.Seeding;
using Kadari.Infrastructure.Security;
using Kadari.Infrastructure.Storage;

namespace Kadari.Infrastructure;

/// <summary>
/// Registers the infrastructure layer: PostgreSQL (EF Core), the outbox-backed bus, S3-compatible blob storage
/// (envelope-encrypted), sign-in security, SMS/WhatsApp code delivery and vision enrichment. The "kadaridb" and
/// "messaging" connection strings are supplied by the Aspire AppHost at run time.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// The Web API's infrastructure: everything shared, plus the boot work only the API does (seeds, bucket creation,
    /// the evidence-retention sweep) and the bus with no consumers — the API publishes through the outbox and ships
    /// it, but never consumes.
    /// </summary>
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        AddShared(builder);
        MessagingExtensions.AddMessaging(builder, configureConsumers: null);

        // Idempotent boot seeds: the consumer-direct default tenant, and the first administrator.
        builder.Services.AddHostedService<DefaultTenantSeeder>();
        builder.Services.Configure<AdministratorSeedOptions>(builder.Configuration.GetSection(AdministratorSeedOptions.SectionName));
        builder.Services.AddHostedService<AdministratorSeeder>();

        // Development only: the demo cast (an insurer, staff and customers with households), when DemoSeed:Enabled.
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.Configure<DemoSeedOptions>(builder.Configuration.GetSection(DemoSeedOptions.SectionName));
            builder.Services.AddHostedService<DemoDataSeeder>();
        }

        // Ensure the bucket exists before anything uploads (idempotent, non-fatal).
        builder.Services.AddHostedService<BlobStorageInitializer>();

        // Evidence retention: purges ephemeral capture frames past their retention date.
        builder.Services.Configure<Tasks.AssetEvidenceRetentionOptions>(
            builder.Configuration.GetSection(Tasks.AssetEvidenceRetentionOptions.SectionName));
        builder.Services.AddHostedService<Tasks.AssetEvidenceRetentionService>();

        return builder;
    }

    /// <summary>
    /// The Worker's infrastructure: everything shared, plus the bus with the Worker's consumers. No seeds and no
    /// API boot work — those stay with the API so two hosts never race to create the same rows.
    /// </summary>
    public static IHostApplicationBuilder AddWorkerInfrastructure(
        this IHostApplicationBuilder builder, Action<IBusRegistrationConfigurator> configureConsumers)
    {
        ArgumentNullException.ThrowIfNull(configureConsumers);
        AddShared(builder);
        MessagingExtensions.AddMessaging(builder, configureConsumers);
        return builder;
    }

    private static void AddShared(IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();   // real transactional UoW (overrides the Application default)
        AddPersistence(builder);
        AddBlobStorage(builder);
        AddSecurity(builder);
        AddVisionEnrichment(builder);
    }

    /// <summary>
    /// Vision enrichment for capture. Provider-agnostic: Gemini or any OpenAI-compatible endpoint when configured,
    /// otherwise the pass-through (the capture keeps the on-device detections), so capture runs without any key.
    /// </summary>
    private static void AddVisionEnrichment(IHostApplicationBuilder builder)
    {
        var config = builder.Configuration;
        builder.Services.Configure<Enrichment.VisionEnricherOptions>(config.GetSection(Enrichment.VisionEnricherOptions.SectionName));
        builder.Services.Configure<Enrichment.GeminiOptions>(config.GetSection(Enrichment.GeminiOptions.SectionName));
        builder.Services.Configure<Enrichment.OpenAiVisionOptions>(config.GetSection(Enrichment.OpenAiVisionOptions.SectionName));
        builder.Services.AddSingleton<Enrichment.VisionConcurrencyGate>();

        var options = new Enrichment.VisionEnricherOptions();
        config.GetSection(Enrichment.VisionEnricherOptions.SectionName).Bind(options);

        switch (options.Provider)
        {
            case Enrichment.VisionProvider.Gemini:
                builder.Services.AddHttpClientWithBudget(Enrichment.GeminiVisionEnricher.HttpClientName, TimeSpan.FromSeconds(options.TimeoutSeconds));
                builder.Services.AddScoped<Application.Features.AnalyzeSnap.IVisionEnricher, Enrichment.GeminiVisionEnricher>();
                break;

            case Enrichment.VisionProvider.OpenAiCompatible:
                builder.Services.AddHttpClientWithBudget(Enrichment.OpenAiCompatibleVisionEnricher.HttpClientName, TimeSpan.FromSeconds(options.TimeoutSeconds));
                builder.Services.AddScoped<Application.Features.AnalyzeSnap.IVisionEnricher, Enrichment.OpenAiCompatibleVisionEnricher>();
                break;

            default:
                builder.Services.AddScoped<Application.Features.AnalyzeSnap.IVisionEnricher, Enrichment.PassThroughVisionEnricher>();
                break;
        }
    }

    private static void AddSecurity(IHostApplicationBuilder builder)
    {
        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
        builder.Services.Configure<Application.Features.Identity.Services.IdentityHashOptions>(
            builder.Configuration.GetSection(Application.Features.Identity.Services.IdentityHashOptions.SectionName));

        builder.Services.AddSingleton<ITokenService, JwtTokenService>();
        builder.Services.AddSingleton<IRefreshTokenService, RefreshTokenService>();
        builder.Services.AddSingleton<IOtpCodeGenerator, OtpCodeGenerator>();
        builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        builder.Services.AddSingleton<Application.Abstractions.Security.ISensitiveDataProtector, FieldProtectorSensitiveDataProtector>();

        // The secrets that protect stored data must be real outside Development.
        builder.Services.AddSecretValidation(builder.Configuration, builder.Environment.IsDevelopment());

        // At-rest field encryption: the process-wide key ring (current + previous keys) from config; the built-in
        // dev key stands when unset.
        FieldProtector.ConfigureFrom(builder.Configuration);

        // Short-lived signed links and tamper-evidence for the asset-for-sale export.
        builder.Services.Configure<ReportMediaOptions>(builder.Configuration.GetSection(ReportMediaOptions.SectionName));
        builder.Services.AddSingleton<IReportMediaLinkFactory, ReportMediaLinkFactory>();
        builder.Services.Configure<ReportIntegrityOptions>(builder.Configuration.GetSection(ReportIntegrityOptions.SectionName));
        builder.Services.AddSingleton<IReportIntegritySigner, HmacReportIntegritySigner>();

        // Email: authenticated SMTP with TLS, when a provider is configured.
        builder.Services.AddOptions<SmtpOptions>()
            .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
            .Validate(
                o => !o.IsConfigured
                     || (!string.IsNullOrWhiteSpace(o.Username) && !string.IsNullOrWhiteSpace(o.Password)
                         && System.Net.Mail.MailAddress.TryCreate(o.FromAddress, out _) && o.Port is > 0 and < 65536),
                "Smtp:Host is set, so Smtp:Username, :Password, a valid :FromAddress and a port are required.")
            .ValidateOnStart();
        if (builder.Configuration.GetSection(SmtpOptions.SectionName).Get<SmtpOptions>()?.IsConfigured == true)
        {
            builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }

        // Sign-in codes: Twilio (SMS or a WhatsApp authentication template) when configured; otherwise the local Mailpit
        // inbox when the AppHost provides one; otherwise the logging stub. Both fallbacks are development-only stubs that
        // the production readiness guard refuses outside Development.
        builder.Services.Configure<TwilioOptions>(builder.Configuration.GetSection(TwilioOptions.SectionName));
        var twilio = builder.Configuration.GetSection(TwilioOptions.SectionName).Get<TwilioOptions>();
        var devMailbox = DevMailboxEndpoint.Parse(builder.Configuration.GetConnectionString("mailpit"));
        if (!string.IsNullOrWhiteSpace(twilio?.AccountSid))
        {
            builder.Services.AddHttpClient(TwilioMessageSender.HttpClientName);
            builder.Services.AddSingleton<TwilioMessageSender>();
            builder.Services.AddSingleton<INotificationChannel, TwilioNotificationChannel>();
            builder.Services.AddSingleton<IOtpDelivery, TwilioOtpDelivery>();
        }
        else if (devMailbox is not null)
        {
            builder.Services.AddSingleton(devMailbox);
            builder.Services.AddSingleton<INotificationChannel, DevMailboxNotificationChannel>();
            builder.Services.AddSingleton<IOtpDelivery, NotificationOtpDelivery>();
        }
        else
        {
            builder.Services.AddSingleton<INotificationChannel, StubNotificationChannel>();
            builder.Services.AddSingleton<IOtpDelivery, NotificationOtpDelivery>();
        }
    }

    private static void AddPersistence(IHostApplicationBuilder builder)
    {
        // Aspire wires the "kadaridb" connection string, health checks and telemetry. The interceptor applies audit
        // stamps and soft-delete on every save.
        builder.AddNpgsqlDbContext<KadariDbContext>(
            "kadaridb",
            configureDbContextOptions: options =>
                options.AddInterceptors(new EntityConventionsInterceptor(TimeProvider.System)));
        builder.Services.AddScoped<IKadariDbContext>(sp => sp.GetRequiredService<KadariDbContext>());

        // Sign-in code countries and the daily vision quota, bound from config.
        builder.Services.Configure<Application.Features.Identity.Services.OtpOptions>(
            builder.Configuration.GetSection(Application.Features.Identity.Services.OtpOptions.SectionName));
        builder.Services.Configure<Application.Features.AnalyzeSnap.VisionQuotaOptions>(
            builder.Configuration.GetSection(Application.Features.AnalyzeSnap.VisionQuotaOptions.SectionName));

    }

    private static void AddBlobStorage(IHostApplicationBuilder builder)
    {
        builder.Services.Configure<S3Options>(builder.Configuration.GetSection(S3Options.SectionName));

        builder.Services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<S3Options>>().Value;

            var config = new AmazonS3Config { ForcePathStyle = options.ForcePathStyle };
            if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
            {
                config.ServiceURL = options.ServiceUrl;
            }

            if (!string.IsNullOrWhiteSpace(options.Region))
            {
                config.AuthenticationRegion = options.Region;
            }

            // Explicit credentials for MinIO/local; the ambient AWS credential chain in production.
            return string.IsNullOrWhiteSpace(options.AccessKey)
                ? new AmazonS3Client(config)
                : new AmazonS3Client(options.AccessKey, options.SecretKey ?? string.Empty, config);
        });

        // Every object is envelope-encrypted before it reaches the bucket; S3 only ever holds ciphertext.
        builder.Services.AddSingleton<S3BlobStorage>();
        builder.Services.AddSingleton<IBlobStorage>(sp => new EnvelopeEncryptedBlobStorage(
            sp.GetRequiredService<S3BlobStorage>(),
            sp.GetRequiredService<Application.Abstractions.Security.ISensitiveDataProtector>()));

    }
}
