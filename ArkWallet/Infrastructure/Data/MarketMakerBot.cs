namespace ArkWallet.Infrastructure.Data;

/// <summary>
/// Бот для создания искусственной рыночной активности
/// </summary>
internal class MarketMakerBotRecord : EntityData
{
    public long Id { get; internal set; }
    public string Symbol { get; internal set; } = string.Empty;
    public long TraderId { get; internal set; }

    /// <summary>Базовая мощность (объём ордера в токенах)</summary>
    public decimal BasePower { get; internal set; }

    /// <summary>Коэффициент отклонения мощности</summary>
    public decimal PowerDeviationCoeff { get; internal set; } = 1m;

    /// <summary>Фактическая мощность после применения модификаторов (объём ордера в токенах)</summary>
    public decimal ActivePower { get; internal set; }

    /// <summary>Роль: покупатель, продавец</summary>
    public BotRole Role { get; internal set; } = 0;

    /// <summary>Время следующего изменения мощности</summary>
    public DateTime NextPowerChange { get; internal set; }

    /// <summary>Время обновления сетки</summary>
    public DateTime NextRebalance { get; internal set; }

    /// <summary>Активен ли бот</summary>
    public bool IsActive { get; internal set; } = true;

    /// <summary>Время создания</summary>
    public DateTime CreatedAt { get; internal set; } = DateTime.UtcNow;

    public static MarketMakerBotRecord Create(long traderId, string symbol, BotRole botRole, decimal initialPower = 50, TimeProvider? timeProvider = null)
    {
        var utcNow = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        return new MarketMakerBotRecord
        {
            TraderId = traderId,
            Symbol = symbol,
            BasePower = initialPower,
            ActivePower = initialPower,
            PowerDeviationCoeff = 1m,
            Role = botRole,
            NextPowerChange = utcNow.AddMinutes(Random.Shared.Next(2, 5)),
        };
    }
}

/// <summary>
/// Роль бота на рынке
/// </summary>
public enum BotRole
{
    /// <summary>Бот выступает в роли покупателя</summary>
    Buyer,

    /// <summary>Бот выступает в роли продавца</summary>
    Seller,

    /// <summary>Бот-«стена» — привязка к трейдеру WallBlocker</summary>
    Waller
}
