using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.TradeOrderServices;

/// <summary>
/// Сервис для отмены торговых ордеров
/// </summary>
public interface IOrderCancellationService
{
    /// <summary>
    /// Отменяет конкретный ордер по его идентификатору
    /// </summary>
    /// <param name="traderId">Telegram ID трейдера, инициирующего отмену</param>
    /// <param name="orderId">Идентификатор ордера для отмены</param>
    /// <returns>Результат операции отмены</returns>
    /// <remarks>
    /// <para>
    /// Выполняет проверки перед отменой:
    /// - Ордер должен существовать
    /// - Ордер должен принадлежать указанному трейдеру
    /// - Ордер должен быть активен (не исполнен и не отменён)
    /// </para>
    /// <para>
    /// После успешной отмены ордер получает статус Cancelled.
    /// Зарезервированные средства возвращаются трейдеру.
    /// </para>
    /// </remarks>
    Task<Result> CancelOrderAsync(long traderId, string orderId);

    /// <summary>
    /// Отменяет все активные ордера трейдера
    /// </summary>
    /// <param name="traderId">Telegram ID трейдера</param>
    /// <returns>Результат операции отмены</returns>
    /// <remarks>
    /// <para>
    /// Находит все активные ордера трейдера и отменяет их.
    /// </para>
    /// <para>
    /// Каждый ордер проверяется на принадлежность трейдеру.
    /// Зарезервированные средства возвращаются трейдеру.
    /// </para>
    /// </remarks>
    Task<Result> CancelAllOrderAsync(long traderId);
}