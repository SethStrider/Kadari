using System.Reflection;
using Kadari.Application.Abstractions.Intelligence;

namespace Kadari.ArchitectureTests;

/// <summary>
/// "All model calls go through one abstraction that records usage and cost" (CARE/16, Ripple §8). Only two types may
/// take the model gateway: <c>MeteredModel</c> in Application (consent, quota, usage) and capture's enricher in
/// Infrastructure, whose usage capture records through the same writer. Anything else injecting the gateway would be
/// an unmetered, unconsented model call.
/// </summary>
public class ModelCallRuleTests
{
    private static readonly string[] Allowed = ["MeteredModel", "ModelVisionEnricher"];

    private static IEnumerable<Type> GatewayConsumers(Assembly assembly) =>
        assembly.GetTypes().Where(t => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(IModelGateway))));

    [Fact]
    public void Only_the_metered_caller_and_the_capture_enricher_take_the_model_gateway()
    {
        var offenders = new[] { typeof(Kadari.Application.DependencyInjection).Assembly, typeof(Kadari.Infrastructure.DependencyInjection).Assembly }
            .SelectMany(GatewayConsumers)
            .Select(t => t.Name)
            .Where(name => !Allowed.Contains(name))
            .ToArray();

        Assert.True(offenders.Length == 0, $"Call models through MeteredModel, not IModelGateway: {string.Join(", ", offenders)}");
    }

    [Fact] // Positive control: the scan does find the allowed consumers.
    public void Detector_is_not_vacuous()
        => Assert.Contains("MeteredModel", GatewayConsumers(typeof(Kadari.Application.DependencyInjection).Assembly).Select(t => t.Name));
}
