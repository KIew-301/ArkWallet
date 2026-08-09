using System.Diagnostics.CodeAnalysis;
using ArkWallet.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Common;

[ExcludeFromCodeCoverage(Justification = "Инфраструктурный обработчик ошибок, catch-блоки не содержат бизнес-логики")]
internal static class ServiceErrorHandler
{
    internal static async Task<Result<T>> ExecuteAsync<T>(
        Func<Task<Result<T>>> action, ILogger logger, string context)
    {
        Result<T> result;
        try
        {
            result = await action();
        }
        catch (DomainException ex)
        {
            result = Result<T>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Context}: {ErrorMessage}", context, ex.Message);
            result = Result<T>.Fail(ex.InnerException?.Message ?? ex.Message);
        }

        ArkWalletMetrics.RecordServiceResult(context, result.IsSuccess, result.Message);
        return result;
    }

    internal static async Task<Result> ExecuteAsync(
        Func<Task<Result>> action, ILogger logger, string context)
    {
        Result result;
        try
        {
            result = await action();
        }
        catch (DomainException ex)
        {
            result = Result.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Context}: {ErrorMessage}", context, ex.Message);
            result = Result.Fail(ex.InnerException?.Message ?? ex.Message);
        }

        ArkWalletMetrics.RecordServiceResult(context, result.IsSuccess, result.Message);
        return result;
    }

    internal static Result<T> Execute<T>(
        Func<Result<T>> action, ILogger logger, string context)
    {
        Result<T> result;
        try
        {
            result = action();
        }
        catch (DomainException ex)
        {
            result = Result<T>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Context}: {ErrorMessage}", context, ex.Message);
            result = Result<T>.Fail(ex.InnerException?.Message ?? ex.Message);
        }

        ArkWalletMetrics.RecordServiceResult(context, result.IsSuccess, result.Message);
        return result;
    }
}
