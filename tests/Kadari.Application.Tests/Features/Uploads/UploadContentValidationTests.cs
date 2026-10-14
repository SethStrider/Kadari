using Kadari.Application.Features.AnalyzeSnap;

namespace Kadari.Application.Tests.Features.Uploads;

/// <summary>GL-18: the capture validator judges the bytes, so a disguised file is refused before it is stored.</summary>
public class UploadContentValidationTests
{
    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg", true)]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/jpeg", false)]   // PNG declared as JPEG
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67, 0x3E }, "image/jpeg", false)]                     // "<svg>"
    public async Task A_snap_must_be_the_image_type_it_declares(byte[] bytes, string contentType, bool valid)
    {
        var path = Path.GetTempFileName();
        await File.WriteAllBytesAsync(path, bytes);
        await using var image = new SnapImage(path, contentType, bytes.Length);

        var result = new AnalyzeSnapValidator().Validate(new AnalyzeSnapCommand(Guid.NewGuid(), image, [], null));

        Assert.Equal(valid, result.IsValid);
    }
}
