using MassTransit;
using Kadari.Application;
using Kadari.Infrastructure;
using Kadari.Infrastructure.ProductionReadiness;

// ---------------------------------------------------------------------------------------------------
// Kadari.Worker — the only consumer of integration events, and the home of scheduled sweeps (CLAUDE.md §6a).
//
//   Consumers   public classes in Consumers/, one per integration event from Kadari.Contracts. A consumer is thin:
//               it does the external call (or none) and records the outcome by sending an Application command.
//   Sweeps      BackgroundServices that dispatch Application commands on a schedule.
//
// The Worker acts as the system, never as a customer: what it sends must be an IUnscopedRequest.
// ---------------------------------------------------------------------------------------------------

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddApplication();
builder.AddWorkerInfrastructure(bus => bus.AddConsumers(typeof(Program).Assembly));

// Scheduled sweeps.
builder.Services.Configure<Kadari.Worker.Sweeps.CareSweepOptions>(
    builder.Configuration.GetSection(Kadari.Worker.Sweeps.CareSweepOptions.SectionName));
builder.Services.AddHostedService<Kadari.Worker.Sweeps.CareReminderSweep>();

var host = builder.Build();

// Same refusal as the API: no placeholder providers or development secrets outside Development.
ProductionReadinessGuard.AssertProductionReady(host.Services, host.Services.GetRequiredService<IHostEnvironment>());

await host.RunAsync();

/// <summary>Exposed so tests can locate the Worker assembly (consumers are discovered from it).</summary>
public partial class Program;
