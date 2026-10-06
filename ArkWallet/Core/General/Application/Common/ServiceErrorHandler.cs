using System.Diagnostics.CodeAnalysis;
using ArkWallet.Core.General.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.General.Application.Common;

[ExcludeFromCodeCoverage(Justification = "Инфраструктурный обработчик ошибок, catch-блоки не содержат бизнес-логики")]
internal static class ServiceErrorHandler
{
    /// <summary>
    /// Отказ домена — ожидаемый исход, а не сбой, но раньше он попадал только в <see cref="Result"/>,
    /// и в логах оставалось голое сообщение без контекста. Теперь причина отказа видна в логе
    /// вместе с контекстом вызова, иначе диагностировать её приходится по коду.
    /// </summary>
    private static void LogDomainRejection(Exception ex, ILogger logger, string context)
        => logger.LogWarning("{Context}: отказ доменного правила — {Reason}", context, ex.Message);

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
            LogDomainRejection(ex, logger, context);
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
            LogDomainRejection(ex, logger, context);
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
            LogDomainRejection(ex, logger, context);
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
