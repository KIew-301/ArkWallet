using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.TraderServices;

/// <summary>
/// Сервис для сохранения снимков баланса в историю
/// </summary>
public interface IBalanceSavingService
{
    /// <summary>
    /// Сохраняет снимок баланса трейдера в базу данных
    /// </summary>
    /// <param name="traderTelegramId">Telegram ID трейдера</param>
    /// <param name="totalBalance">Полный баланс (сумма всех компонентов)</param>
    /// <param name="mainBalance">Основной баланс (деньги на счете)</param>
    /// <param name="longOrderReserve">Резерв в Long-ордерах</param>
    /// <param name="shortOrderReserve">Резерв в Short-ордерах</param>
    /// <param name="balanceInTokens">Стоимость токенов в портфеле</param>
    /// <param name="snapshotDateTime">Дата и время снимка (UTC)</param>
    /// <returns>Результат операции сохранения</returns>
    /// <remarks>
    /// <para>
    /// Выполняет проверки перед сохранением:
    /// - Дата и время снимка не должна быть значением по умолчанию
    /// - Создаёт сущность BalanceSnapshot через фабричный метод
    /// </para>
    /// <para>
    /// Использует транзакцию для обеспечения целостности данных.
    /// </para>
    /// </remarks>
    Task<Result> SaveBalanceToDatabase(
        long traderTelegramId,
        decimal totalBalance,
        decimal mainBalance,
        decimal longOrderReserve,
        decimal shortOrderReserve,
        decimal balanceInTokens,
        DateTime snapshotDateTime);
}