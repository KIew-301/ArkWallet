using ArkWallet.Domain.Common;
using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Domain.TradingContext;

/// <summary>Редкость токена: от одной до шести звёзд.</summary>
public enum TokenRarity
{
    /// <summary>Редкость: одна звезда.</summary>
    OneStar = 1,

    /// <summary>Редкость: две звезды.</summary>
    TwoStar = 2,

    /// <summary>Редкость: три звезды.</summary>
    ThreeStar = 3,

    /// <summary>Редкость: четыре звезды.</summary>
    FourStar = 4,

    /// <summary>Редкость: пять звёзд.</summary>
    FiveStar = 5,

    /// <summary>Редкость: шесть звёзд.</summary>
    SixStar = 6
}

internal class Token : AggregateRoot
{
    private const int PriceCandleTimeframeMinutes = 1;

    private readonly List<PriceCandle> _priceHistory = new();

    public string Symbol { get; }
    public string Name { get; }
    public TokenRarity Rarity { get; }
    public decimal CurrentPrice { get; private set; }
    public int TotalSupply { get; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; }
    public string ImageUrl { get; private set; }
    public string IconUrl { get; private set; }

    public IReadOnlyList<PriceCandle> PriceHistory => _priceHistory;

    private Token(
        string symbol,
        string name,
        TokenRarity rarity,
        decimal initialPrice,
        int totalSupply,
        string imageUrl,
        string iconUrl,
        DateTime createdAt)
    {
        Symbol = symbol;
        Name = name;
        Rarity = rarity;
        CurrentPrice = initialPrice;
        TotalSupply = totalSupply;
        IsActive = true;
        CreatedAt = createdAt;
        ImageUrl = imageUrl;
        IconUrl = iconUrl;
    }

    public static Token Create(
        string symbol,
        string name,
        TokenRarity rarity,
        decimal initialPrice,
        int totalSupply,
        string imageUrl,
        string iconUrl,
        TimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new DomainException("Token symbol cannot be empty");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Token name cannot be empty");
        if (initialPrice <= 0)
            throw new DomainException("Initial price must be greater than 0");
        if (totalSupply <= 0)
            throw new DomainException("Total supply must be greater than 0");
        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new DomainException("Image URL cannot be empty");
        if (string.IsNullOrWhiteSpace(iconUrl))
            throw new DomainException("Icon URL cannot be empty");

        var createdAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        return new Token(symbol, name, rarity, initialPrice, totalSupply, imageUrl, iconUrl, createdAt);
    }

    internal static Token Load(
        string symbol,
        string name,
        TokenRarity rarity,
        decimal currentPrice,
        int totalSupply,
        bool isActive,
        string imageUrl,
        string iconUrl,
        DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new DomainException("Token symbol cannot be empty");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Token name cannot be empty");
        if (currentPrice < 0)
            throw new DomainException("Price cannot be negative");
        if (totalSupply <= 0)
            throw new DomainException("Total supply must be greater than 0");

        var token = new Token(symbol, name, rarity, currentPrice, totalSupply, imageUrl, iconUrl, createdAt);
        token.IsActive = isActive;
        return token;
    }

    public bool CanBeTraded() => IsActive && TotalSupply > 0;

    public void UpdateMedia(string iconUrl, string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(iconUrl))
            throw new DomainException("Icon URL cannot be empty");
        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new DomainException("Image URL cannot be empty");

        IconUrl = iconUrl;
        ImageUrl = imageUrl;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public async Task UpdatePrice(decimal newPrice, DateTime timestamp)
    {
        if (newPrice < 0)
            throw new DomainException("Price cannot be negative");

        CurrentPrice = newPrice;
        RecordPrice(newPrice, timestamp);
        await PublishAsync(new TokenPriceUpdatedEvent(this));
    }

    private void RecordPrice(decimal newPrice, DateTime timestamp)
    {
        var lastCandle = _priceHistory.LastOrDefault();
        if (lastCandle is null)
        {
            _priceHistory.Add(PriceCandle.CreateNew(newPrice, timestamp));
        }
        else if (lastCandle.Timestamp.AddMinutes(PriceCandleTimeframeMinutes) <= timestamp)
        {
            var candle = PriceCandle.CreateNew(lastCandle.ClosePrice, timestamp);
            candle.Update(newPrice);
            _priceHistory.Add(candle);
        }
        else
        {
            lastCandle.Update(newPrice);
        }
    }

    public decimal CalculateMarketCap() => CurrentPrice * TotalSupply;
}
