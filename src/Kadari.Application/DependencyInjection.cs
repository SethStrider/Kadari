using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Access;
using Kadari.Application.Behaviors;
using Kadari.Application.Common;

namespace Kadari.Application;

/// <summary>
/// Registers the application layer: MediatR command/query handlers, FluentValidation validators,
/// and the cross-cutting pipeline behaviours.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

        // Handlers may log (e.g. a refused settlement). Idempotent: the hosts have already registered it.
        services.AddLogging();

        // Validators are discovered by convention; add a slice's validator and it just works.
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // Pipeline order (outer → inner): validate first (fail fast, before any transaction opens),
        // then prove the caller owns the resource (PAS-002 §2 — a refused request must not open a
        // transaction), then run the command in a transaction and dispatch its domain events inside it.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ResourceAuthorizationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionalDispatchBehavior<,>));
        services.AddScoped<Behaviors.PostCommitActions>();
        services.AddScoped<Abstractions.Persistence.IPostCommitActions>(sp => sp.GetRequiredService<Behaviors.PostCommitActions>());

        // Resource-owner resolvers live beside the slice they serve; discovered by convention like
        // validators, so adding a scoped slice needs no DI edit.
        services.AddResourceOwnerResolvers(assembly);

        services.AddScoped<ICurrentCustomer, CurrentCustomer>();

        // The tenant partition the caller acts within (D2/D11). TryAdd, so an insurer-aware resolver can win.
        services.TryAddScoped<ICurrentTenant, DefaultTenant>();

        // A host with no authenticated caller (seeders, slice tests) still needs an ICurrentUser: unauthenticated,
        // so the fallback fails closed. The WebApi's JWT-backed implementation wins where one exists.
        services.TryAddScoped<ICurrentUser, UnauthenticatedUser>();

        // Defaults for what Infrastructure fulfils in the composed app (in-memory hosts and slice tests).
        services.TryAddScoped<IUnitOfWork, PassThroughUnitOfWork>();
        services.TryAddScoped<IIntegrationEventPublisher, NoOpIntegrationEventPublisher>();
        services.TryAddSingleton<Abstractions.Security.ISensitiveDataProtector, PassThroughSensitiveDataProtector>();

        // Asset-export tamper-evidence: a keyless fallback for hosts without Infrastructure (slice tests); the HMAC signer
        // (server key) replaces it in the composed app.
        services.TryAddSingleton<Abstractions.Reports.IReportIntegritySigner, Common.UnkeyedReportIntegritySigner>();

        // Capture (AnalyzeSnap): once-only external work per capture, and the daily vision quota (AS-09).
        services.AddScoped<Features.AnalyzeSnap.SnapCaptureWork>();
        services.AddScoped<Features.AnalyzeSnap.VisionQuota>();
        services.AddOptions<Features.AnalyzeSnap.VisionQuotaOptions>();

        // Assets: the one place asset-level ownership is decided for the asset and Care slices.
        services.AddScoped<Features.Assets.Services.OwnedAssets>();
        services.AddScoped<Features.Assets.Services.PurchaseReader>();

        // KADARI Care: the coverage engine runner (shared by enable, re-evaluate and the evidence reaction), and the
        // registry importer behind preview-then-apply.
        services.AddOptions<Features.Care.Services.CareOptions>();
        services.AddScoped<Features.Care.Services.CareCoverage>();
        services.AddScoped<Features.Care.Services.CareReminders>();
        services.AddScoped<Features.Care.Services.OwnedReminders>();
        services.AddScoped<Features.CareRegistry.Services.RegistryImporter>();

        // AI: the one way to call a model (consent, quota, once-per-request, usage + cost), and the one usage writer.
        services.AddOptions<Features.Intelligence.Services.ModelPricingOptions>();
        services.AddScoped<Features.Intelligence.Services.ModelUsageRecorder>();
        services.AddScoped<Features.Intelligence.Services.MeteredModel>();
        services.TryAddSingleton<Abstractions.Intelligence.IModelGateway, Common.NoModelGateway>();

        // Owners: the customer -> owning subject bridge (get-or-create).
        services.AddScoped<Features.Owners.Services.OwnerRegistrar>();

        // Valuation rails: the router picks the first rail that values an asset's category, in registration
        // order. The professional rail claims jewellery/art (declines to auto-value them); the AI baseline covers
        // the rest as a self-asserted estimate.
        services.AddScoped<Features.Valuation.IValuationRail, Features.Valuation.ProfessionalValuationRail>();
        services.AddScoped<Features.Valuation.IValuationRail, Features.Valuation.AiBaselineValuationRail>();
        services.AddScoped<Features.Valuation.ValuationRailRouter>();

        // Which tenant a customer's assets belong to: their insurer cohort if assigned, else the default tenant.
        services.AddScoped<Features.Tenancy.TenantMembershipResolver>();
        services.AddScoped<Features.Tenancy.InsurerTenantGuard>();
        services.AddScoped<Features.DataRights.KadariErasure>();
        services.AddScoped<Features.DataRights.KadariDataExport>();

        // Sign-in: OTP rules (ZA-only by default) and the identity-key hasher; Infrastructure binds config.
        services.AddOptions<Features.Identity.Services.IdentityHashOptions>();
        services.AddOptions<Features.Identity.Services.OtpOptions>();
        services.AddSingleton<Abstractions.Identity.IIdentityKeyHasher, Features.Identity.Services.HmacIdentityKeyHasher>();

        return services;
    }
}
