using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.SettleCaseAppointment;

/// <summary>An appointment happened (<c>Done</c>) or was cancelled. Customer-scoped.</summary>
public sealed record SettleCaseAppointmentCommand(Guid CustomerId, Guid AssetId, Guid CaseId, Guid AppointmentId, bool Done)
    : ICommand<CaseDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class SettleCaseAppointmentHandler(CaseCommands cases) : ICommandHandler<SettleCaseAppointmentCommand, CaseDto>
{
    public async Task<CaseDto> Handle(SettleCaseAppointmentCommand cmd, CancellationToken ct)
    {
        var (asset, c) = await cases.LoadAsync(cmd.CustomerId, cmd.AssetId, cmd.CaseId, ct);
        if (cmd.Done)
        {
            c.CompleteAppointment(new CaseAppointmentId(cmd.AppointmentId), cases.Now);
        }
        else
        {
            c.CancelAppointment(new CaseAppointmentId(cmd.AppointmentId), cases.Now);
        }
        return await cases.SaveAsync(asset, c, ct);
    }
}
