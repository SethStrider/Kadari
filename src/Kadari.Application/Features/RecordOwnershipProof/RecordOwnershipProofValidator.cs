using FluentValidation;
using Kadari.Domain.Assets;
using Kadari.Application.Common.Files;

namespace Kadari.Application.Features.RecordOwnershipProof;

internal sealed class RecordOwnershipProofValidator : AbstractValidator<RecordOwnershipProofCommand>
{
    public RecordOwnershipProofValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();

        // At least one proof must be supplied — otherwise there is nothing to record.
        RuleFor(x => x)
            .Must(x => x.Receipt is not null || !string.IsNullOrWhiteSpace(x.SerialNumber))
            .WithMessage("Provide a receipt document or a serial number.");

        RuleFor(x => x.AmountMinor)
            .GreaterThan(0)
            .When(x => x.AmountMinor is not null)
            .WithMessage("A receipt amount must be positive.");

        // A household item over R5m is a typo or an attempt (AS-14); the valuation rail handles real outliers.
        RuleFor(x => x.AmountMinor)
            .LessThanOrEqualTo(MaxAmountMinor)
            .When(x => x.AmountMinor is not null)
            .WithMessage("A receipt amount can be at most R5,000,000.");

        RuleFor(x => x.SerialNumber)
            .Must(serial => SerialNumbers.Normalise(serial) is not null)
            .When(x => !string.IsNullOrWhiteSpace(x.SerialNumber))
            .WithMessage($"A serial number needs 3 to {SerialNumbers.MaxLength} letters or digits.");

        // An independently-supported value needs its supporting document — a price with no receipt is
        // self-asserted, which is the capture path, not this one.
        RuleFor(x => x.Receipt)
            .NotNull()
            .When(x => x.AmountMinor is not null)
            .WithMessage("A receipt amount requires the receipt document that supports it.");

        // A receipt is a PDF or a photo — judged by its bytes, not its name (GL-18).
        RuleFor(x => x.Receipt)
            .Must(receipt => IsReceiptFile(receipt!))
            .When(x => x.Receipt is { Length: > 0 })
            .WithMessage("A receipt must be a PDF, JPEG, PNG or WebP file.");

        // Launch is South Africa only (AS-14); widen when another market opens.
        RuleFor(x => x.Currency)
            .Equal("ZAR")
            .When(x => !string.IsNullOrWhiteSpace(x.Currency))
            .WithMessage("Receipt amounts must be in rand (ZAR).");
    }

    /// <summary>R5,000,000 in cents.</summary>
    private const long MaxAmountMinor = 500_000_000;

    private static bool IsReceiptFile(ReceiptDocument receipt)
    {
        using var stream = receipt.OpenRead();
        return FileSignature.Detect(stream) is FileKind.Pdf or FileKind.Jpeg or FileKind.Png or FileKind.WebP;
    }
}
