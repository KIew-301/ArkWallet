namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>
/// Модификатор пересчёта активной мощности бота.
/// Отключаемое расширение: получает ссылку на бота и меняет его ActivePower.
/// </summary>
public interface IPowerCalculationModify
{
    /// <summary>Applies the power calculation modification to the given bot.</summary>
    /// <param name="bot">The market maker bot whose active power will be updated.</param>
    void Apply(MarketMakerBot bot);
}
