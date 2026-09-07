using ArkWallet.Domain.Common;
using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Domain.GiftContext;

/// <summary>
/// User aggregate in the Gift context. The sender is the root of gifting rules: it selects the
/// token to gift from its own portfolio, removes it as part of sending and applies the rate
/// limit. The resulting gift-sent event is consumed by the Mail context to create the gift message.
/// </summary>
internal class User : AggregateRoot
{
    private const decimal MaxTokenPrice = 1000m;
    private static readonly TimeSpan GiftCooldown = TimeSpan.FromHours(8);

    private readonly List<Tokens> _portfolio;

    public long Id { get; }
    public string Name { get; }
    public IReadOnlyList<Tokens> Portfolio => _portfolio;
    public IReadOnlyList<SentGift> SentGifts { get; }

    private User(long id, string name, List<Tokens> portfolio, IReadOnlyList<SentGift> sentGifts)
    {
        Id = id;
        Name = name;
        _portfolio = portfolio;
        SentGifts = sentGifts;
    }

    /// <summary>
    /// Rehydrates a User (the gift sender) with all its portfolio tokens and sent gifts.
    /// </summary>
    internal static User Load(
        long id,
        string name,
        List<Tokens> portfolio,
        IReadOnlyList<SentGift> sentGifts)
    {
        return new User(id, name, portfolio, sentGifts);
    }

    /// <summary>
    /// Sends a gift to the recipient. Takes only the recipient and the current time; everything
    /// else (portfolio, rate limit, sender) comes from the aggregate itself. Removes the gifted
    /// token from its own portfolio.
    /// </summary>
    public async Task<GiftSentEvent> SendGift(long recipientId, DateTime createdAt)
    {
        if (Id == recipientId)
            throw new DomainException("Нельзя отправить подарок самому себе");

        var cooldownStart = createdAt - GiftCooldown;
        var alreadyGifted = SentGifts.Any(g => g.RecipientId == recipientId && g.SentAt > cooldownStart);
        if (alreadyGifted)
            throw new DomainException("Нельзя отправлять более 1 токена одному человеку раз в 8 часов");

        var eligible = _portfolio
            .Where(t => t.Price <= MaxTokenPrice && !t.IsEmpty)
            .ToList();

        if (eligible.Count == 0)
            throw new DomainException("Нет подходящих токенов в портфеле (все токены дороже лимита или портфель пуст)");

        var selected = eligible[Random.Shared.Next(eligible.Count)];
        selected.RemoveQuantity(1);

        if (selected.IsEmpty)
            _portfolio.Remove(selected);

        var giftSent = new GiftSentEvent(
            Id,
            recipientId,
            Name,
            selected.Symbol,
            1,
            createdAt);

        await PublishAsync(giftSent);

        return giftSent;
    }
}

/// <summary>
/// A token in the sender's portfolio that can be gifted. Mutable because gifting removes tokens.
/// </summary>
internal sealed class Tokens
{
    public string Symbol { get; }
    public int Quantity { get; private set; }
    public decimal Price { get; }

    internal Tokens(string symbol, int quantity, decimal price)
    {
        Symbol = symbol;
        Quantity = quantity;
        Price = price;
    }

    public bool IsEmpty => Quantity <= 0;

    internal void RemoveQuantity(int amount)
    {
        Quantity -= amount;
    }
}

/// <summary>
/// A gift the sender has already sent, used for the per-recipient cooldown.
/// </summary>
internal readonly record struct SentGift(long RecipientId, DateTime SentAt);
