using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Domain.TradingContext;

/// <summary>Роль бота на рынке: покупатель или продавец.</summary>
public enum MarketMakerRole
{
    /// <summary>Бот действует в роли покупателя.</summary>
    Buyer,

    /// <summary>Бот действует в роли продавца.</summary>
    Seller
}

internal class MarketMaker
{
    public long Id { get; private set; }
    public long TraderId { get; }
    public string Symbol { get; }
    public decimal BasePower { get; private set; }
    public MarketMakerRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; }

    private MarketMaker(long traderId, string symbol, MarketMakerRole role, decimal basePower, DateTime createdAt)
    {
        TraderId = traderId;
        Symbol = symbol;
        Role = role;
        BasePower = basePower;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public static MarketMaker Create(long traderId, string symbol, MarketMakerRole role, decimal initialPower = 50m, TimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new DomainException("Symbol cannot be empty");
        if (initialPower <= 0)
            throw new DomainException("Initial power must be greater than 0");

        var createdAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        return new MarketMaker(traderId, symbol, role, initialPower, createdAt);
    }

    public void UpdatePower(decimal minPower, decimal maxPower)
    {
        if (minPower >= maxPower)
            throw new DomainException("Min power must be less than max power");

        var change = Random.Shared.Next(-35, 35);
        BasePower = Math.Clamp(BasePower + change, minPower, maxPower);
    }

    public void SetRole(MarketMakerRole role) => Role = role;

    public void SetActive(bool isActive) => IsActive = isActive;
}
