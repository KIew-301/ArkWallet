namespace ArkWallet.Application.Contracts.Other;

/// <summary>
/// Сервис получения текущего снапшота метрик приложения в формате Prometheus
/// </summary>
public interface IMetricsSnapshotService
{
    /// <summary>
    /// Возвращает текст метрик в формате Prometheus exposition
    /// </summary>
    Task<string> GetMetricsTextAsync();
}
