namespace Kadari.Application.Abstractions.Messaging;

/// <summary>
/// Marks a request as a state-changing command (both <see cref="ICommand"/> and
/// <see cref="ICommand{TResponse}"/>). The pipeline uses it to run commands — and only commands —
/// inside a transaction; queries pass through untouched.
/// </summary>
public interface IBaseCommand;
