using FluentValidation;
using MediatR;
using ValidationException = Kadari.Application.Exceptions.ValidationException;

namespace Kadari.Application.Behaviors;

/// <summary>
/// MediatR pipeline step that runs every registered <see cref="IValidator{T}"/> for the request
/// before the handler executes (fail fast). Aggregates failures into an application
/// <see cref="ValidationException"/> keyed by property name.
/// <para>Requests with no registered validator pass straight through.</para>
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!validators.Any())
        {
            return await next(ct);
        }

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct)));

        var errors = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.Distinct().ToArray());

        if (errors.Count != 0)
        {
            throw new ValidationException(errors);
        }

        return await next(ct);
    }
}
