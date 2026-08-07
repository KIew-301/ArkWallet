using ArkWallet.Application.Common;
using ArkWallet.Application.Services.TraderServices;

namespace ArkWallet.Application.Contracts.TraderServices;
/// <summary>
/// Сервис для расчёта изменений баланса трейдера за период
/// </summary>
public interface IBalanceChangesCalculationService
{
    /// <summary>
    /// Рассчитывает изменение основного баланса (денежные средства) за указанный период
    /// </summary>
    /// <param name="traderTelegramId">Telegram ID трейдера</param>
    /// <param name="periodDays">Количество дней для расчёта (минимум 1, максимум 365)</param>
    /// <returns>Данные об изменении основного баланса</returns>
    /// <remarks>
    /// <para>
    /// Расчёт выполняется на основе:
    /// - Текущего основного баланса (денежные средства на счете)
    /// - Исторического снапшота основного баланса за указанный период
    /// </para>
    /// <para>
    /// Если исторический снапшот отсутствует, используется значение по умолчанию (1000).
    /// </para>
    /// </remarks>
    Task<Result<BalanceChangesData>> TakeMainBalanceChanges(long traderTelegramId, int periodDays);

    /// <summary>
    /// Рассчитывает изменение полного баланса (деньги + токены) за указанный период
    /// </summary>
    /// <param name="traderTelegramId">Telegram ID трейдера</param>
    /// <param name="periodDays">Количество дней для расчёта (минимум 1, максимум 365)</param>
    /// <returns>Данные об изменении полного баланса</returns>
    /// <remarks>
    /// <para>
    /// Расчёт выполняется на основе:
    /// - Текущего полного баланса (денежные средства + стоимость токенов в портфеле + резервы)
    /// - Исторического снапшота полного баланса за указанный период
    /// </para>
    /// <para>
    /// Полный баланс включает:
    /// - Основной баланс (деньги на счете)
    /// - Стоимость токенов в портфеле по текущей цене
    /// - Резервы в активных ордерах
    /// </para>
    /// <para>
    /// Если исторический снапшот отсутствует, используется значение по умолчанию (1000).
    /// </para>
    /// </remarks>
    Task<Result<BalanceChangesData>> TakeTotalBalanceChanges(long traderTelegramId, int periodDays);

    /// <summary>
    /// Рассчитывает изменение основного и полного баланса за период одним прогоном текущего снапшота
    /// </summary>
    /// <param name="traderTelegramId">Telegram ID трейдера</param>
    /// <param name="periodDays">Количество дней для расчёта (минимум 1, максимум 365)</param>
    /// <returns>Изменения основного и полного баланса</returns>
    Task<Result<BalanceChangesBundle>> TakeBalanceChanges(long traderTelegramId, int periodDays);
}
/// <summary>
/// Данные об изменении баланса за период
/// </summary>
/// <param name="CurrentBalance">Текущий баланс</param>
/// <param name="PreviousBalance">Баланс за предыдущий период</param>
/// <param name="ChangeAbsolute">Абсолютное изменение баланса</param>
/// <param name="ChangePercent">Процентное изменение баланса</param>
public record BalanceChangesData(
    decimal CurrentBalance,
    decimal PreviousBalance,
    decimal ChangeAbsolute,
    decimal ChangePercent
);

/// <summary>
/// Изменения основного и полного баланса за период (текущий снапшот считается один раз)
/// </summary>
/// <param name="Main">Изменение основного баланса</param>
/// <param name="Total">Изменение полного баланса</param>
public record BalanceChangesBundle(BalanceChangesData Main, BalanceChangesData Total);