namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>
/// Модификатор плана бота: источник ордеров или трансформатор количества.
/// Конвейерный контракт — получает текущий план и возвращает новый (порядок применения имеет значение).
/// Источники добавляют команды на размещение ордеров, трансформаторы (случайность/импульс/коррекции) меняют quantity.
/// Результат маппится в Application-команду <c>CreateOrderCommand</c> на границе слоя.
/// </summary>
internal interface IPlanModify
{
    /// <summary>Данные рынка, необходимые модификатору. Оркестратор мержит маски и собирает их пакетно.</summary>
    MarketDataMasks RequiredMarketData { get; }

    IReadOnlyCollection<CreateMarketOrderCommand> Build(
        MarketMakerBot bot,
        MarketConditions market,
        IReadOnlyCollection<CreateMarketOrderCommand> currentPlan);
}
