namespace ArkWallet.Domain.Entities;

/// <summary>
/// Бот для создания искусственной рыночной активности
/// </summary>
internal class MarketMakerBot
{
    public long Id { get; private set; }
    public string Symbol { get; private set; } = string.Empty;
    public long TraderId { get; private set; }

    /// <summary>Базовая мощность (объём ордера в токенах)</summary>
    public decimal BasePower { get; private set; }

    /// <summary>Роль: покупатель, продавец</summary>
    public BotRole Role { get; private set; } = 0;

    /// <summary>Время следующего изменения мощности</summary>
    public DateTime NextPowerChange { get; private set; }

    /// <summary>Время обновления сетки</summary>
    public DateTime NextRebalance { get; private set; }

    /// <summary>Активен ли бот</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Время создания</summary>
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public static MarketMakerBot Create(long traderId, string symbol, BotRole botRole, decimal initialPower = 50, TimeProvider? timeProvider = null)
    {
        var utcNow = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        return new MarketMakerBot
        {
            TraderId = traderId,
            Symbol = symbol,
            BasePower = initialPower,
            Role = botRole,
            NextPowerChange = utcNow.AddMinutes(Random.Shared.Next(2, 5)),
        };
    }

    /// <summary>Обновляет мощность случайным образом</summary>
    public void UpdatePower(decimal minPower, decimal maxPower, TimeProvider? timeProvider = null)
    {
        var change = Random.Shared.Next(-35, 35);
        BasePower = Math.Clamp(BasePower + change, minPower, maxPower);
        NextPowerChange = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime.AddMinutes(Random.Shared.Next(2, 5));
    }

    /// <summary>Обновляет время обновления сетки</summary>
    public void UpdateRebalanced(TimeProvider? timeProvider = null)
    {
        NextRebalance = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime.AddMinutes(10);
    }

    /// <summary>Устанавливает роль бота</summary>
    public void SetRole(BotRole role)
    {
        Role = role;
    }

    /// <summary>Устанавливает флаг активности бота</summary>
    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }

    /// <summary>Устанавливает мощность напрямую</summary>
    public void SetBasePower(decimal power)
    {
        BasePower = power;
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
    Seller
}