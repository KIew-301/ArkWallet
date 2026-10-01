namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>
/// Модификатор пересчёта активной мощности бота.
/// Отключаемое расширение: получает ссылку на бота и меняет его ActivePower.
/// </summary>
public interface IPowerCalculationModify
{
    /// <summary>
    /// Применяет модификатор расчёта мощности к указанному боту, обновляя его ActivePower.
    /// </summary>
    /// <param name="bot">Бот-маркетмейкер, у которого будет пересчитана активная мощность.</param>
    void Apply(MarketMakerBot bot);
}
