namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>
/// Модификатор пересчёта активной мощности бота.
/// Отключаемое расширение: получает ссылку на бота и меняет его ActivePower.
/// </summary>
public interface IPowerCalculationModify
{
    void Apply(MarketMakerBot bot);
}
