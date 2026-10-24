namespace Kadari.Application.Features.Care.Services;

/// <summary>Care requests take enum <em>names</em> ("Fault"), since JSON enums bind by ordinal here (CLAUDE.md §6b).
/// A number, a combination or an unknown name is not accepted.</summary>
internal static class CareEnum
{
    public static bool IsName<TEnum>(string? value) where TEnum : struct, Enum => TryParse<TEnum>(value, out _);

    public static bool TryParse<TEnum>(string? value, out TEnum parsed) where TEnum : struct, Enum
    {
        parsed = default;
        return !string.IsNullOrWhiteSpace(value) && !int.TryParse(value, out _) && !value.Contains(',')
               && Enum.TryParse(value.Trim(), ignoreCase: true, out parsed) && Enum.IsDefined(parsed);
    }

    public static TEnum Parse<TEnum>(string value) where TEnum : struct, Enum =>
        TryParse<TEnum>(value, out var parsed) ? parsed : throw new ArgumentException($"'{value}' is not a {typeof(TEnum).Name}.");

    public static string Names<TEnum>(params TEnum[] allowed) where TEnum : struct, Enum =>
        string.Join(", ", (allowed.Length == 0 ? Enum.GetValues<TEnum>() : allowed).Select(v => v.ToString()));
}
