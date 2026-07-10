using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.TraderServices;

/// <summary>
/// Оркестратор для создания снимков баланса всех трейдеров
/// </summary>
public interface IBalanceSnapshotOrchestrator
{
    /// <summary>
    /// Создаёт и сохраняет снимки баланса для всех трейдеров
    /// </summary>
    /// <returns>Результат операции</returns>
    /// <remarks>
    /// <para>
    /// Для каждого трейдера:
    /// 1. Создаёт снимок баланса через IBalanceSnapshotService
    /// 2. Сохраняет его через IBalanceSavingService
    /// </para>
    /// <para>
    /// Операция выполняется в транзакции. При любой ошибке все изменения откатываются.
    /// </para>
    /// </remarks>
    Task<Result> CreateSnapshotsForAllTradersAsync();
}
