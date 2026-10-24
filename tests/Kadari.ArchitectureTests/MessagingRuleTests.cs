using System.Reflection;
using MassTransit;

namespace Kadari.ArchitectureTests;

/// <summary>
/// The bus rules (CLAUDE.md §6a): integration-event contracts depend on nothing, MassTransit stays out of the inner
/// layers, and consumers live only in the Worker — public, so MassTransit can build them.
/// </summary>
public class MessagingRuleTests
{
    private static readonly Assembly Domain = typeof(Kadari.Domain.AssemblyReference).Assembly;
    private static readonly Assembly Application = typeof(Kadari.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Kadari.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly Contracts = typeof(Kadari.Contracts.AssemblyReference).Assembly;
    private static readonly Assembly Worker = typeof(Program).Assembly;

    private static string[] ReferencedNames(Assembly a) =>
        a.GetReferencedAssemblies().Select(n => n.Name!).ToArray();

    private static bool IsConsumer(Type type) =>
        type is { IsClass: true, IsAbstract: false }
        && type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>));

    private static IEnumerable<Type> ConsumersIn(Assembly assembly) => assembly.GetTypes().Where(IsConsumer);

    [Fact]
    public void Contracts_reference_no_Kadari_project_and_no_package()
    {
        // Only the .NET base library (System.*): records compile against System.Runtime and System.Collections.
        var unexpected = ReferencedNames(Contracts)
            .Where(n => !(n == "netstandard" || n.StartsWith("System.", StringComparison.Ordinal)))
            .ToArray();

        Assert.True(unexpected.Length == 0, $"Kadari.Contracts must stay dependency-free: {string.Join(", ", unexpected)}");
    }

    [Fact]
    public void Domain_does_not_reference_Contracts()
        => Assert.DoesNotContain("Kadari.Contracts", ReferencedNames(Domain));

    [Fact]
    public void Application_does_not_reference_MassTransit()
        => Assert.DoesNotContain(ReferencedNames(Application), n => n.StartsWith("MassTransit", StringComparison.Ordinal));

    [Fact]
    public void No_layer_references_the_Worker()
    {
        foreach (var layer in new[] { Domain, Application, Infrastructure, Contracts })
        {
            Assert.DoesNotContain("Kadari.Worker", ReferencedNames(layer));
        }
    }

    [Fact]
    public void Consumers_live_only_in_the_Worker()
    {
        var misplaced = new[] { Domain, Application, Infrastructure, Contracts }
            .SelectMany(ConsumersIn)
            .Select(t => t.FullName)
            .ToArray();

        Assert.True(misplaced.Length == 0, $"Consumers belong in Kadari.Worker: {string.Join(", ", misplaced)}");
    }

    [Fact]
    public void Worker_consumers_are_public()
    {
        var hidden = ConsumersIn(Worker).Where(t => !t.IsPublic).Select(t => t.FullName).ToArray();

        Assert.True(hidden.Length == 0, $"Consumers must be public: {string.Join(", ", hidden)}");
    }

    [Fact] // Positive control: the consumer detector recognises a consumer, so the rules above cannot pass vacuously.
    public void Consumer_detector_is_not_vacuous()
        => Assert.True(IsConsumer(typeof(ProbeConsumer)));

    private sealed class ProbeConsumer : IConsumer<ProbeMessage>
    {
        public Task Consume(ConsumeContext<ProbeMessage> context) => Task.CompletedTask;
    }

    private sealed record ProbeMessage;
}
