namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>
/// Поля рыночных условий, которые требуются модификатору плана.
/// Оркестратор объединяет маски всех модификаторов коллекции и собирает данные минимальным числом запросов.
/// </summary>
[Flags]
internal enum MarketDataMask
{
    None = 0,
    CurrentPrice = 1,
    DayAgoPrice = 2,
    BasePrice = 4,
    ExistingLevels = 8,
}