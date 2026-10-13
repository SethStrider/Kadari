namespace Kadari.Application.Common.Files;

/// <summary>What an upload's first bytes say it is — regardless of its name or declared content type.</summary>
public enum FileKind
{
    Unknown,
    Pdf,
    Jpeg,
    Png,
    WebP,

    /// <summary>A ZIP container — the only shape a .docx can have.</summary>
    Zip,
}

/// <summary>
/// Magic-byte detection for uploads (GL-18). A file name and a Content-Type header are both chosen by the client;
/// the leading bytes are not, so an HTML or script file cannot be stored as a "passport.pdf" or an "image/jpeg".
/// </summary>
public static class FileSignature
{
    /// <summary>Enough leading bytes to tell every supported kind apart.</summary>
    public const int HeaderLength = 12;

    public static FileKind Detect(ReadOnlySpan<byte> header) => header switch
    {
        [0x25, 0x50, 0x44, 0x46, 0x2D, ..] => FileKind.Pdf,                                  // %PDF-
        [0xFF, 0xD8, 0xFF, ..] => FileKind.Jpeg,
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => FileKind.Png,
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => FileKind.WebP,   // RIFF....WEBP
        [0x50, 0x4B, 0x03, 0x04, ..] => FileKind.Zip,                                         // PK\x03\x04
        _ => FileKind.Unknown,
    };

    /// <summary>Reads the header of a seekable or forward-only stream and restores nothing — open a fresh stream.</summary>
    public static FileKind Detect(Stream stream)
    {
        Span<byte> header = stackalloc byte[HeaderLength];
        var read = stream.ReadAtLeast(header, HeaderLength, throwOnEndOfStream: false);
        return Detect(header[..read]);
    }

    /// <summary>The content type a detected image kind must be declared as.</summary>
    public static string? ImageContentType(FileKind kind) => kind switch
    {
        FileKind.Jpeg => "image/jpeg",
        FileKind.Png => "image/png",
        FileKind.WebP => "image/webp",
        _ => null,
    };
}
