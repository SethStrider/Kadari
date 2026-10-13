using FluentValidation;
using Kadari.Application.Common.Files;

namespace Kadari.Application.Features.AnalyzeSnap;

internal sealed class AnalyzeSnapValidator : AbstractValidator<AnalyzeSnapCommand>
{
    // The device sends at most this many boxes per frame; slack on top of that bounds the enricher prompt.
    private const int MaxDetections = 32;

    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];

    public AnalyzeSnapValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();

        RuleFor(c => c.Image).NotNull();
        RuleFor(c => c.Image.Length).GreaterThan(0).WithMessage("An image is required.");
        RuleFor(c => c.Image.ContentType)
            .Must(ct => AllowedContentTypes.Contains(ct))
            .WithMessage($"Image must be one of: {string.Join(", ", AllowedContentTypes)}.");

        // The declared type is the client's claim; the leading bytes are the image (GL-18). Every still is stored
        // as evidence and may be sent to the vision model, so it must really be one.
        RuleFor(c => c.Image)
            .Must(image => DeclaredTypeMatchesContent(image))
            .When(c => c.Image is { Length: > 0 })
            .WithMessage("The image's contents do not match its declared type.");

        RuleFor(c => c.Detections)
            .NotNull()
            .Must(d => d.Count <= MaxDetections)
            .WithMessage($"At most {MaxDetections} detections are accepted.");

        RuleForEach(c => c.Detections).ChildRules(detection =>
        {
            detection.RuleFor(d => d.Label).NotEmpty().MaximumLength(120);
            detection.RuleFor(d => d.Confidence).InclusiveBetween(0, 1);
            // Boxes are normalised by the client; anything outside the frame is an upstream bug, and
            // silently clamping it here would hide it.
            detection.RuleFor(d => d.X).InclusiveBetween(0, 1);
            detection.RuleFor(d => d.Y).InclusiveBetween(0, 1);
            detection.RuleFor(d => d.Width).InclusiveBetween(0, 1);
            detection.RuleFor(d => d.Height).InclusiveBetween(0, 1);
        });

        RuleFor(c => c.RoomLabel).MaximumLength(60);
    }

    private static bool DeclaredTypeMatchesContent(SnapImage image)
    {
        using var stream = image.OpenRead();
        return FileSignature.ImageContentType(FileSignature.Detect(stream)) == image.ContentType;
    }
}
