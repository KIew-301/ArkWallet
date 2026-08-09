using ArkWallet.Application.Common;
using ArkWallet.Application.Services.TraderServices;

namespace ArkWallet.Application.Contracts.TraderServices;

/// <summary>
/// Сервис для создания снимков полного баланса трейдера
/// </summary>
public interface IBalanceSnapshotService
{
    /// <summary>
    /// Создаёт снимок полного баланса трейдера на текущий момент
    /// </summary>
    /// <param name="traderTelegramId">Telegram ID трейдера</param>
    /// <returns>Результат операции с деталями баланса</returns>
    /// <remarks>
    /// <para>
    /// Снимок включает:
    /// - Основной баланс (деньги на счете)
    /// - Резерв в Long-ордерах (замороженные средства на покупку)
    /// - Резерв в Short-ордерах (стоимость токенов на продажу)
    /// - Стоимость токенов в портфеле
    /// - Итоговый полный баланс
    /// </para>
    /// <para>
    /// Цены токенов берутся из таблицы CharacterTokens.
    /// Если для какого-то токена цена отсутствует, он не учитывается в расчёте.
    /// </para>
    /// </remarks>
    Task<Result<BalanceSnapshotData>> TakeTotalTraderBalanceSnapshot(long traderTelegramId);

    /// <summary>
    /// Создаёт снимки полного баланса для нескольких трейдеров одним набором запросов
    /// </summary>
    /// <param name="traderTelegramIds">Telegram ID трейдеров</param>
    /// <returns>Результат операции со словарём снимков по ID трейдера</returns>
    Task<Result<IReadOnlyDictionary<long, BalanceSnapshotData>>> TakeTotalTraderBalanceSnapshotsAsync(IEnumerable<long> traderTelegramIds);
}