using MediatR;

namespace Kadari.Application.Abstractions.Messaging;

/// <summary>Handles a command that returns no value.</summary>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand>
    where TCommand : ICommand;

/// <summary>Handles a command that returns a result.</summary>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>;
