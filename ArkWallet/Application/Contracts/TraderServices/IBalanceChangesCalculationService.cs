using ArkWallet.Application.Common;
using ArkWallet.Application.Services.TraderServices;

namespace ArkWallet.Application.Contracts.TraderServices;

/// <summary>
/// Сервис для расчёта изменений баланса трейдера за период
/// </summary>
public interface IBalanceChangesCalculationService
{
    /// <summary>
    /// Рассчитывает изменения основного баланса трейдера за указанный период
    /// </summary>
    /// <param name="traderTelegramId">Telegram ID трейдера</param>
    /// <param name="periodDays">Количество дней для расчёта (минимум 1)</param>
    /// <returns>Результат с данными об изменении баланса</returns>
    /// <remarks>
    /// <para>
    /// Расчёт выполняется на основе:
    /// - Текущего снапшота баланса (через IBalanceSnapshotService)
    /// - Исторического снапшота за указанный период (из БД)
    /// </para>
    /// <para>
    /// Если исторический снапшот отсутствует, используется значение по умолчанию (1000).
    /// </para>
    /// </remarks>
    Task<Result<BalanceChangesData>> TakeMainBalanceChanges(long traderTelegramId, int periodDays);
}