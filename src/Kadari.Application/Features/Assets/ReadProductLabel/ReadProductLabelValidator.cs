using FluentValidation;
using Kadari.Application.Common.Files;
using Kadari.Application.Features.AnalyzeSnap;

namespace Kadari.Application.Features.Assets.ReadProductLabel;

internal sealed class ReadProductLabelValidator : AbstractValidator<ReadProductLabelCommand>
{
    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];

    public ReadProductLabelValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.AssetId).NotEmpty();
        RuleFor(c => c.Photo).NotNull();
        RuleFor(c => c.Photo.Length).GreaterThan(0).WithMessage("A photo is required.");
        RuleFor(c => c.Photo.ContentType)
            .Must(ct => AllowedContentTypes.Contains(ct))
            .WithMessage($"The photo must be one of: {string.Join(", ", AllowedContentTypes)}.");

        // The declared type is the client's claim; the leading bytes are the image (GL-18). It is sent to a model.
        RuleFor(c => c.Photo)
            .Must(DeclaredTypeMatchesContent)
            .When(c => c.Photo is { Length: > 0 })
            .WithMessage("The photo's contents do not match its declared type.");
    }

    private static bool DeclaredTypeMatchesContent(SnapImage image)
    {
        using var stream = image.OpenRead();
        return FileSignature.ImageContentType(FileSignature.Detect(stream)) == image.ContentType;
    }
}
