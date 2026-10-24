using Kadari.Domain.Assets;

namespace Kadari.Domain.Care;

public readonly record struct ExternalActionAuditId(Guid Value)
{
    public static ExternalActionAuditId New() => new(Guid.CreateVersion7());
}

/// <summary>Who started an external action.</summary>
public enum ExternalActor
{
    Owner,
    Operator,
    System,
}

/// <summary>
/// The append-only record of one material external action (CARE/12 §8, CARE/03 §11): what was done, to which
/// destination, under which authority, by whom, what was shared (a summary, never the documents) and what came back.
/// Never updated, never soft-deleted.
/// </summary>
public sealed class ExternalActionAudit
{
    public const int MaxTextLength = 500;

    private ExternalActionAudit() { }   // EF

    public ExternalActionAuditId Id { get; private init; }
    public AssetId AssetId { get; private init; }
    public Guid? CaseId { get; private init; }
    public ExternalActionType ActionType { get; private init; }
    public ExternalActionAuthorisationId? AuthorisationId { get; private init; }
    public ExternalActor Actor { get; private init; }
    public Guid? ActorId { get; private init; }
    public string Destination { get; private init; } = null!;
    public string Step { get; private init; } = null!;
    public string? SharedSummary { get; private init; }
    public string? Result { get; private init; }
    public string? ExternalReference { get; private init; }
    public DateTimeOffset OccurredAt { get; private init; }

    public static ExternalActionAudit Record(
        AssetId assetId, ExternalActionType actionType, ExternalActionAuthorisationId? authorisationId, ExternalActor actor,
        Guid? actorId, string destination, string step, DateTimeOffset occurredAt,
        string? sharedSummary = null, string? result = null, string? externalReference = null, Guid? caseId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(step);

        return new ExternalActionAudit
        {
            Id = ExternalActionAuditId.New(),
            AssetId = assetId,
            CaseId = caseId,
            ActionType = actionType,
            AuthorisationId = authorisationId,
            Actor = actor,
            ActorId = actorId,
            Destination = Clip(destination)!,
            Step = Clip(step)!,
            SharedSummary = Clip(sharedSummary),
            Result = Clip(result),
            ExternalReference = Clip(externalReference),
            OccurredAt = occurredAt,
        };
    }

    private static string? Clip(string? value) =>
        value is null ? null : value.Trim() is var t && t.Length > MaxTextLength ? t[..MaxTextLength] : value.Trim();
}
