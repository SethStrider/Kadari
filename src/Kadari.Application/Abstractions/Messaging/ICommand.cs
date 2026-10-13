using MediatR;

namespace Kadari.Application.Abstractions.Messaging;

/// <summary>A command that mutates state and returns no value.</summary>
public interface ICommand : IRequest, IBaseCommand;

/// <summary>A command that mutates state and returns a result.</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>, IBaseCommand;
