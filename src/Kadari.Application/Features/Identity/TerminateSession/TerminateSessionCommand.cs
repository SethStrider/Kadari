using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;

namespace Kadari.Application.Features.Identity.TerminateSession;

/// <summary>Terminate one of the caller's own sessions (logout / revoke a device).</summary>
public sealed record TerminateSessionCommand(Guid SessionId, Guid UserAccountId) : ICommand, IUnscopedRequest;
