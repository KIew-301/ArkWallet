namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>
/// Рыночные условия для выполнения плана бота: собираются Application-оркестратором и передаются модификаторам.
/// </summary>
/// <param name="CurrentPrice">Текущая цена токена бота.</param>
/// <param name="DayAgoPrice">Цена за день до текущего момента (открытие последней свечи старше 24 ч), null — данных нет.</param>
/// <param name="BasePrice">Базовая цена из истории: цена открытия самой первой свечи токена, null — свечей нет.</param>
/// <param name="ExistingLevels">Уровни активных ордеров токена (для дедупликации сеток).</param>
internal sealed record MarketConditions(
    decimal CurrentPrice,
    decimal? DayAgoPrice,
    decimal? BasePrice,
    IReadOnlyCollection<PlacedOrderLevel> ExistingLevels);
