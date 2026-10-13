using MediatR;

namespace Kadari.Application.Abstractions.Messaging;

/// <summary>A read-only query that returns a result.</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;
