using MediatR;
using Kadari.Domain.Abstractions;

namespace Kadari.Application.Abstractions.Messaging;

/// <summary>
/// Wraps a framework-free <see cref="IDomainEvent"/> as a MediatR notification so in-process handlers
/// can react to it — without the Domain depending on MediatR.
/// </summary>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : IDomainEvent;
