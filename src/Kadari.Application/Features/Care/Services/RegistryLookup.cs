using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.CareRegistry;

namespace Kadari.Application.Features.Care.Services;

/// <summary>Finds the registry's brand and retailer for the names the owner's purchase facts carry. Matching is by slug
/// (case and punctuation ignored); a name the registry does not know is simply unknown — never a guess.</summary>
internal sealed class RegistryLookup(IKadariDbContext db)
{
    public async Task<Brand?> BrandAsync(string? name, CancellationToken ct) =>
        SlugOf(name) is { } slug ? await db.Set<Brand>().AsNoTracking().FirstOrDefaultAsync(b => b.Slug == slug, ct) : null;

    public async Task<Retailer?> RetailerAsync(string? name, string country, CancellationToken ct) =>
        SlugOf(name) is { } slug
            ? await db.Set<Retailer>().AsNoTracking().FirstOrDefaultAsync(r => r.Slug == slug && r.CountryCode == country, ct)
            : null;

    private static string? SlugOf(string? name) =>
        string.IsNullOrWhiteSpace(name) || !name.Any(char.IsAsciiLetterOrDigit) ? null : RegistrySlug.For(name);
}
