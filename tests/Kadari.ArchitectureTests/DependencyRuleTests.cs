using System.Reflection;

namespace Kadari.ArchitectureTests;

/// <summary>
/// Encodes the CLAUDE.md §1.2 dependency rules as tests: <c>WebApi → Application → Domain</c>,
/// Infrastructure implements the abstractions, Domain references nothing.
/// <para>
/// We inspect each compiled assembly's referenced-assembly list. The C# compiler keeps a reference
/// only when a type from it is actually used, so an <em>unused</em> project reference cannot sneak a
/// forbidden dependency past these tests.
/// </para>
/// </summary>
public class DependencyRuleTests
{
    private static readonly Assembly Domain = typeof(Kadari.Domain.AssemblyReference).Assembly;
    private static readonly Assembly Application = typeof(Kadari.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Kadari.Infrastructure.DependencyInjection).Assembly;

    private static string[] ReferencedNames(Assembly a) =>
        a.GetReferencedAssemblies().Select(n => n.Name!).ToArray();

    private static void AssertDoesNotReference(Assembly subject, params string[] forbidden)
    {
        var refs = ReferencedNames(subject);
        var violations = forbidden.Where(f => refs.Contains(f)).ToArray();

        Assert.True(
            violations.Length == 0,
            $"{subject.GetName().Name} must not reference: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Domain_references_no_other_Trustli_layer()
        => AssertDoesNotReference(Domain,
            "Kadari.Application", "Kadari.Infrastructure", "Kadari.WebApi");

    [Fact]
    public void Domain_has_no_framework_dependencies()   // no EF, MediatR, MassTransit, ASP.NET
        => AssertDoesNotReference(Domain,
            "Microsoft.EntityFrameworkCore", "MediatR", "MassTransit", "Microsoft.AspNetCore.App");

    [Fact]
    public void Application_does_not_reference_Infrastructure_or_WebApi()
        => AssertDoesNotReference(Application, "Kadari.Infrastructure", "Kadari.WebApi");

    // NOTE: Application MAY reference EF Core — CLAUDE.md §6 has it expose DbSets via
    // IKadariDbContext. The rule is Application must not reference *Infrastructure* (the concrete
    // DbContext, providers, migrations), enforced above. EF the abstraction is allowed.

    [Fact]
    public void Infrastructure_does_not_reference_WebApi()
        => AssertDoesNotReference(Infrastructure, "Kadari.WebApi");


    [Fact] // Positive control: proves the detector actually sees dependencies, so the rules
            // above cannot pass vacuously (e.g. if GetReferencedAssemblies ever returned nothing).
            // Note: only *used* references appear — the compiler trims unused project references,
            // which is precisely why the negative rules above are trustworthy.
    public void Detector_is_not_vacuous()
        => Assert.Contains("MediatR", ReferencedNames(Application));
}
