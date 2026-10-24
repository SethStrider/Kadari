using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.BookCaseAppointment;

/// <summary>A visit, collection or drop-off was booked (CARE/14 C11). Customer-scoped.</summary>
public sealed record BookCaseAppointmentCommand(Guid CustomerId, Guid AssetId, Guid CaseId, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string? Location, string? Instructions)
    : ICommand<CaseDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class BookCaseAppointmentHandler(CaseCommands cases) : ICommandHandler<BookCaseAppointmentCommand, CaseDto>
{
    public async Task<CaseDto> Handle(BookCaseAppointmentCommand cmd, CancellationToken ct)
    {
        var (asset, c) = await cases.LoadAsync(cmd.CustomerId, cmd.AssetId, cmd.CaseId, ct);
        c.BookAppointment(cmd.StartsAt, cmd.EndsAt, cmd.Location, cmd.Instructions, cases.Now);
        return await cases.SaveAsync(asset, c, ct);
    }
}
