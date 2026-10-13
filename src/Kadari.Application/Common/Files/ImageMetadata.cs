using System.Buffers.Binary;

namespace Kadari.Application.Common.Files;

/// <summary>
/// Removes the metadata a photo carries about where and how it was taken (AS-06): EXIF (GPS position, camera,
/// timestamps, often a thumbnail), XMP, IPTC and comments. A photo of someone's lounge must not also store their
/// home's coordinates. Rewrites the container without decoding the image, so pixels and quality are untouched.
/// <list type="bullet">
/// <item><b>JPEG</b>: drops APP1 (EXIF/XMP), APP3–APP13, APP15 and COM. Keeps JFIF (APP0), the ICC colour profile
/// (APP2) and Adobe (APP14), which govern how the image renders. The EXIF orientation is carried over in a minimal
/// EXIF block of its own, or a phone photo would show sideways.</item>
/// <item><b>PNG</b>: drops eXIf, tEXt, zTXt, iTXt and tIME.</item>
/// <item><b>WebP</b>: drops EXIF and XMP chunks, clears their VP8X flags and fixes the RIFF size.</item>
/// </list>
/// Anything else (a PDF receipt, an unknown file) is copied unchanged.
/// </summary>
public static class ImageMetadata
{
    private const ushort OrientationTag = 0x0112;

    /// <summary>
    /// Copies <paramref name="source"/> into a self-deleting temp file with its metadata removed, and returns that
    /// file positioned at the start. The image is streamed, never held whole in memory.
    /// </summary>
    public static async Task<Stream> StripToTempFileAsync(Stream source, CancellationToken ct)
    {
        var output = new FileStream(
            Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None,
            bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        try
        {
            await StripAsync(source, output, ct);
            output.Position = 0;
            return output;
        }
        catch
        {
            await output.DisposeAsync();
            throw;
        }
    }

    /// <summary>Rewrites the file at <paramref name="path"/> without its metadata (a no-op for non-images).</summary>
    public static async Task StripFileInPlaceAsync(string path, CancellationToken ct)
    {
        var stripped = path + ".clean";
        try
        {
            await using var source = File.OpenRead(path);
            await using var destination = File.Create(stripped);
            await StripAsync(source, destination, ct);
        }
        catch (InvalidDataException)
        {
            File.Delete(stripped);
            throw new Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                ["image"] = ["The image could not be read."],
            });
        }

        File.Move(stripped, path, overwrite: true);
    }

    /// <summary>Writes <paramref name="source"/> to <paramref name="destination"/> without its metadata.
    /// <paramref name="destination"/> must be seekable (WebP's RIFF size is patched at the end).</summary>
    public static async Task StripAsync(Stream source, Stream destination, CancellationToken ct)
    {
        var header = new byte[FileSignature.HeaderLength];
        var read = await source.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        var head = header.AsMemory(0, read);

        switch (FileSignature.Detect(head.Span))
        {
            case FileKind.Jpeg:
                await StripJpegAsync(new PrefixedReader(head, source), destination, ct);
                break;
            case FileKind.Png:
                await StripPngAsync(new PrefixedReader(head, source), destination, ct);
                break;
            case FileKind.WebP:
                await StripWebPAsync(new PrefixedReader(head, source), destination, ct);
                break;
            default:
                await destination.WriteAsync(head, ct);
                await source.CopyToAsync(destination, ct);
                break;
        }
    }

    private static async Task StripJpegAsync(PrefixedReader input, Stream output, CancellationToken ct)
    {
        await output.WriteAsync(await input.ReadExactlyAsync(2, ct), ct);   // SOI

        var orientationWritten = false;
        while (true)
        {
            var marker = await input.ReadByteAsync(ct);
            if (marker < 0)
            {
                return;   // truncated — keep what we have
            }

            if (marker != 0xFF)
            {
                throw new InvalidDataException("Malformed JPEG: expected a marker.");
            }

            var code = await input.ReadByteAsync(ct);
            while (code == 0xFF)
            {
                code = await input.ReadByteAsync(ct);   // fill bytes
            }

            if (code < 0)
            {
                return;
            }

            // Start of scan: everything after is image data — copy the rest verbatim.
            if (code == 0xDA)
            {
                await output.WriteAsync(new byte[] { 0xFF, 0xDA }, ct);
                await input.CopyRestToAsync(output, ct);
                return;
            }

            // Markers with no length field.
            if (code is 0x01 or (>= 0xD0 and <= 0xD9))
            {
                await output.WriteAsync(new byte[] { 0xFF, (byte)code }, ct);
                continue;
            }

            // A segment cut short is the end of a truncated file: keep what came before it.
            var lengthBytes = await input.TryReadPartialAsync(2, ct);
            if (lengthBytes is null)
            {
                return;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(lengthBytes);
            if (length < 2 || await input.TryReadPartialAsync(length - 2, ct) is not { } payload)
            {
                return;
            }

            var isApp = code is >= 0xE0 and <= 0xEF;
            var keep = code switch
            {
                0xE0 => true,                                   // JFIF
                0xE2 => StartsWith(payload, "ICC_PROFILE\0"u8),  // colour profile, not the FlashPix junk
                0xEE => true,                                   // Adobe: colour transform
                0xFE => false,                                  // comment
                _ => !isApp,                                    // every other APPn is metadata
            };

            if (code == 0xE1 && !orientationWritten && ExifOrientation(payload) is { } orientation and not 1)
            {
                await output.WriteAsync(MinimalExif(orientation), ct);
                orientationWritten = true;
            }

            if (!keep)
            {
                continue;
            }

            await output.WriteAsync(new byte[] { 0xFF, (byte)code }, ct);
            await output.WriteAsync(lengthBytes, ct);
            await output.WriteAsync(payload, ct);
        }
    }

    private static readonly HashSet<string> PngMetadataChunks = ["eXIf", "tEXt", "zTXt", "iTXt", "tIME"];

    private static async Task StripPngAsync(PrefixedReader input, Stream output, CancellationToken ct)
    {
        await output.WriteAsync(await input.ReadExactlyAsync(8, ct), ct);   // signature

        while (true)
        {
            var header = await input.TryReadExactlyAsync(8, ct);
            if (header is null)
            {
                return;
            }

            var length = BinaryPrimitives.ReadUInt32BigEndian(header);
            var type = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            if (PngMetadataChunks.Contains(type))
            {
                await input.SkipAsync(length + 4, ct);   // data + CRC
                continue;
            }

            await output.WriteAsync(header, ct);
            await input.CopyAsync(output, length + 4, ct);
            if (type == "IEND")
            {
                return;
            }
        }
    }

    private static async Task StripWebPAsync(PrefixedReader input, Stream output, CancellationToken ct)
    {
        var riff = await input.ReadExactlyAsync(12, ct);   // "RIFF" size "WEBP"
        var start = output.Position;
        await output.WriteAsync(riff, ct);

        while (true)
        {
            var header = await input.TryReadExactlyAsync(8, ct);
            if (header is null)
            {
                break;
            }

            var fourCc = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4));
            var padded = size + (size & 1);

            if (fourCc is "EXIF" or "XMP ")
            {
                await input.SkipAsync(padded, ct);
                continue;
            }

            await output.WriteAsync(header, ct);
            if (fourCc == "VP8X")
            {
                var data = await input.ReadExactlyAsync((int)padded, ct);
                data[0] &= unchecked((byte)~(0x08 | 0x04));   // clear the EXIF and XMP flags
                await output.WriteAsync(data, ct);
            }
            else
            {
                await input.CopyAsync(output, padded, ct);
            }
        }

        // The RIFF size is everything after its own 8-byte header.
        var end = output.Position;
        var riffSize = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(riffSize, (uint)(end - start - 8));
        output.Position = start + 4;
        await output.WriteAsync(riffSize, ct);
        output.Position = end;
    }

    /// <summary>The EXIF orientation (1–8) in an APP1 payload, or null when there is none.</summary>
    internal static int? ExifOrientation(ReadOnlySpan<byte> app1)
    {
        if (!StartsWith(app1, "Exif\0\0"u8) || app1.Length < 14)
        {
            return null;
        }

        var tiff = app1[6..];
        var little = tiff[0] == (byte)'I';
        ushort U16(ReadOnlySpan<byte> s) => little ? BinaryPrimitives.ReadUInt16LittleEndian(s) : BinaryPrimitives.ReadUInt16BigEndian(s);
        uint U32(ReadOnlySpan<byte> s) => little ? BinaryPrimitives.ReadUInt32LittleEndian(s) : BinaryPrimitives.ReadUInt32BigEndian(s);

        var ifd = (int)U32(tiff[4..]);
        if (ifd + 2 > tiff.Length)
        {
            return null;
        }

        var count = U16(tiff[ifd..]);
        for (var i = 0; i < count; i++)
        {
            var entry = ifd + 2 + (i * 12);
            if (entry + 12 > tiff.Length)
            {
                return null;
            }

            if (U16(tiff[entry..]) == OrientationTag)
            {
                var value = U16(tiff[(entry + 8)..]);
                return value is >= 1 and <= 8 ? value : null;
            }
        }

        return null;
    }

    /// <summary>An APP1 segment holding only the orientation tag (big-endian TIFF, one IFD0 entry).</summary>
    private static byte[] MinimalExif(int orientation)
    {
        byte[] tiff =
        [
            (byte)'M', (byte)'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08,   // header, IFD0 at 8
            0x00, 0x01,                                                // one entry
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01,             // Orientation, SHORT, count 1
            0x00, (byte)orientation, 0x00, 0x00,                        // value
            0x00, 0x00, 0x00, 0x00,                                     // no next IFD
        ];
        var length = 2 + 6 + tiff.Length;
        return [0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. "Exif\0\0"u8, .. tiff];
    }

    private static bool StartsWith(ReadOnlySpan<byte> data, ReadOnlySpan<byte> prefix) => data.StartsWith(prefix);

    /// <summary>A forward reader over the sniffed header followed by the rest of the stream.</summary>
    private sealed class PrefixedReader(ReadOnlyMemory<byte> prefix, Stream rest)
    {
        private ReadOnlyMemory<byte> _prefix = prefix;

        public async Task<int> ReadByteAsync(CancellationToken ct)
        {
            var one = await TryReadExactlyAsync(1, ct);
            return one is null ? -1 : one[0];
        }

        public async Task<byte[]> ReadExactlyAsync(int count, CancellationToken ct) =>
            await TryReadExactlyAsync(count, ct) ?? throw new InvalidDataException("The image ended unexpectedly.");

        /// <summary>Reads exactly <paramref name="count"/> bytes, or null at a clean end of input.</summary>
        public async Task<byte[]?> TryReadExactlyAsync(int count, CancellationToken ct)
        {
            var buffer = new byte[count];
            var fromPrefix = Math.Min(count, _prefix.Length);
            _prefix[..fromPrefix].CopyTo(buffer);
            _prefix = _prefix[fromPrefix..];

            var read = fromPrefix + await rest.ReadAtLeastAsync(
                buffer.AsMemory(fromPrefix), count - fromPrefix, throwOnEndOfStream: false, ct);
            if (read == 0)
            {
                return null;
            }

            return read == count ? buffer : throw new InvalidDataException("The image ended unexpectedly.");
        }

        /// <summary>Exactly <paramref name="count"/> bytes, or null if the input ends first.</summary>
        public async Task<byte[]?> TryReadPartialAsync(int count, CancellationToken ct)
        {
            try
            {
                return await TryReadExactlyAsync(count, ct);
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }

        public async Task SkipAsync(long count, CancellationToken ct)
        {
            var buffer = new byte[81920];
            while (count > 0)
            {
                var chunk = await TryReadExactlyAsync((int)Math.Min(count, buffer.Length), ct)
                    ?? throw new InvalidDataException("The image ended unexpectedly.");
                count -= chunk.Length;
            }
        }

        public async Task CopyAsync(Stream output, long count, CancellationToken ct)
        {
            while (count > 0)
            {
                var chunk = await TryReadExactlyAsync((int)Math.Min(count, 81920), ct)
                    ?? throw new InvalidDataException("The image ended unexpectedly.");
                await output.WriteAsync(chunk, ct);
                count -= chunk.Length;
            }
        }

        public async Task CopyRestToAsync(Stream output, CancellationToken ct)
        {
            await output.WriteAsync(_prefix, ct);
            _prefix = ReadOnlyMemory<byte>.Empty;
            await rest.CopyToAsync(output, ct);
        }
    }
}
