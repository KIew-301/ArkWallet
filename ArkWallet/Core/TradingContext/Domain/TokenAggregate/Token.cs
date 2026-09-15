using ArkWallet.Core.General.Domain.Common;
using ArkWallet.Core.General.Domain.Exceptions;
using ArkWallet.Core.TradingContext.Domain.Events;

namespace ArkWallet.Core.TradingContext.Domain.TokenAggregate;

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

internal record TokenLoadCommand(
    string Symbol,
    string Name,
    TokenRarity Rarity,
    decimal CurrentPrice,
    int TotalSupply,
    bool IsActive,
    string ImageUrl,
    string IconUrl,
    DateTime CreatedAt);

internal record TokenCreationCommand(
    string Symbol,
    string Name,
    TokenRarity Rarity,
    decimal InitialPrice,
    int TotalSupply,
    string ImageUrl,
    string IconUrl);

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

    private Token(TokenCreationCommand cmd, DateTime createdAt)
    {
        Symbol = cmd.Symbol;
        Name = cmd.Name;
        Rarity = cmd.Rarity;
        CurrentPrice = cmd.InitialPrice;
        TotalSupply = cmd.TotalSupply;
        IsActive = true;
        CreatedAt = createdAt;
        ImageUrl = cmd.ImageUrl;
        IconUrl = cmd.IconUrl;
    }

    public static Token Create(TokenCreationCommand cmd, TimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(cmd.Symbol))
            throw new DomainException("Token symbol cannot be empty");
        if (string.IsNullOrWhiteSpace(cmd.Name))
            throw new DomainException("Token name cannot be empty");
        if (cmd.InitialPrice <= 0)
            throw new DomainException("Initial price must be greater than 0");
        if (cmd.TotalSupply <= 0)
            throw new DomainException("Total supply must be greater than 0");
        if (string.IsNullOrWhiteSpace(cmd.ImageUrl))
            throw new DomainException("Image URL cannot be empty");
        if (string.IsNullOrWhiteSpace(cmd.IconUrl))
            throw new DomainException("Icon URL cannot be empty");

        var createdAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        return new Token(cmd, createdAt);
    }

    internal static Token Load(TokenLoadCommand data)
    {
        if (string.IsNullOrWhiteSpace(data.Symbol))
            throw new DomainException("Token symbol cannot be empty");
        if (string.IsNullOrWhiteSpace(data.Name))
            throw new DomainException("Token name cannot be empty");
        if (data.CurrentPrice < 0)
            throw new DomainException("Price cannot be negative");
        if (data.TotalSupply <= 0)
            throw new DomainException("Total supply must be greater than 0");

        var token = new Token(new TokenCreationCommand(data.Symbol, data.Name, data.Rarity, data.CurrentPrice, data.TotalSupply, data.ImageUrl, data.IconUrl), data.CreatedAt);
        token.IsActive = data.IsActive;
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
