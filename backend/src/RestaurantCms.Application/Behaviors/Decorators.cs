using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Common;
using RestaurantCms.Domain.Common;

namespace RestaurantCms.Application.Behaviors;

// Cross-cutting behaviour wrapped around every handler by the DI registration:
//   Logging → Validation → Handler

public sealed class ValidatingCommandHandler<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public Task<Result<TResponse>> Handle(TCommand command, CancellationToken ct)
    {
        var errors = ValidationRunner.Run(validators, command);
        return errors is null ? inner.Handle(command, ct) : Task.FromResult(Result<TResponse>.Failure(errors));
    }
}

public sealed class ValidatingQueryHandler<TQuery, TResponse>(
    IQueryHandler<TQuery, TResponse> inner,
    IEnumerable<IValidator<TQuery>> validators) : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public Task<Result<TResponse>> Handle(TQuery query, CancellationToken ct)
    {
        var errors = ValidationRunner.Run(validators, query);
        return errors is null ? inner.Handle(query, ct) : Task.FromResult(Result<TResponse>.Failure(errors));
    }
}

public sealed class LoggingCommandHandler<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    ILogger<LoggingCommandHandler<TCommand, TResponse>> logger) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken ct)
    {
        var name = typeof(TCommand).DeclaringType?.Name ?? typeof(TCommand).Name;
        var started = Stopwatch.GetTimestamp();
        var result = await inner.Handle(command, ct);
        var ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (result.IsSuccess)
            logger.LogInformation("{UseCase} succeeded in {ElapsedMs:0} ms", name, ms);
        else
            logger.LogWarning("{UseCase} failed in {ElapsedMs:0} ms: {ErrorCode} {ErrorMessage}", name, ms, result.Error!.Code, result.Error.Message);
        return result;
    }
}

internal static class ValidationRunner
{
    public static Error? Run<T>(IEnumerable<IValidator<T>> validators, T instance)
    {
        var merged = new Dictionary<string, string[]>();
        foreach (var v in validators)
            foreach (var (field, messages) in v.Validate(instance))
                merged[field] = merged.TryGetValue(field, out var existing) ? [.. existing, .. messages] : messages;
        return merged.Count == 0 ? null : Error.Invalid(merged);
    }
}
