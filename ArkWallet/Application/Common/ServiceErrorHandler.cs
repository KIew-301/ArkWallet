using ArkWallet.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Common;

internal static class ServiceErrorHandler
{
    internal static async Task<Result<T>> ExecuteAsync<T>(
        Func<Task<Result<T>>> action, ILogger logger, string context)
    {
        try
        {
            return await action();
        }
        catch (DomainException ex)
        {
            return Result<T>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Context}: {ErrorMessage}", context, ex.Message);
            return Result<T>.Fail(ex.InnerException?.Message ?? ex.Message);
        }
    }

    internal static async Task<Result> ExecuteAsync(
        Func<Task<Result>> action, ILogger logger, string context)
    {
        try
        {
            return await action();
        }
        catch (DomainException ex)
        {
            return Result.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Context}: {ErrorMessage}", context, ex.Message);
            return Result.Fail(ex.InnerException?.Message ?? ex.Message);
        }
    }

    internal static Result<T> Execute<T>(
        Func<Result<T>> action, ILogger logger, string context)
    {
        try
        {
            return action();
        }
        catch (DomainException ex)
        {
            return Result<T>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Context}: {ErrorMessage}", context, ex.Message);
            return Result<T>.Fail(ex.InnerException?.Message ?? ex.Message);
        }
    }
}
