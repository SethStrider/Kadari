using MediatR;

namespace Kadari.Application.Abstractions.Messaging;

/// <summary>Handles a query and returns a result.</summary>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>;
