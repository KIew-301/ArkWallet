using ArkWallet.Domain.Common;
using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Domain.GiftContext;

internal class User : AggregateRoot
{
    private readonly List<PortfolioPosition> _portfolio = new();
    private readonly List<SentGift> _giftsSent = new();
    private readonly List<ReceivedGift> _giftsReceived = new();

    public long Id { get; }
    public IReadOnlyList<PortfolioPosition> Portfolio => _portfolio;
    public IReadOnlyList<SentGift> GiftsSent => _giftsSent;
    public IReadOnlyList<ReceivedGift> GiftsReceived => _giftsReceived;

    private const decimal MaxTokenPrice = 1000m;

    private User(long id)
    {
        Id = id;
    }

    public static User Create(long id)
    {
        return new User(id);
    }

    internal static User Load(
        long id,
        List<PortfolioPosition> portfolio,
        List<SentGift> giftsSent,
        List<ReceivedGift> giftsReceived)
    {
        var user = new User(id);
        user._portfolio.AddRange(portfolio);
        user._giftsSent.AddRange(giftsSent);
        user._giftsReceived.AddRange(giftsReceived);
        return user;
    }

    public async Task SendGift(
        long recipientId,
        IReadOnlyDictionary<string, decimal> tokenPrices,
        TimeProvider timeProvider)
    {
        if (Id == recipientId)
            throw new DomainException("Нельзя отправить подарок самому себе");

        var eightHoursAgo = timeProvider.GetUtcNow().UtcDateTime.AddHours(-8);
        var recentGift = _giftsSent.FirstOrDefault(g =>
            g.RecipientId == recipientId
            && g.SentAt > eightHoursAgo);
        if (recentGift is not null)
            throw new DomainException($"Нельзя отправлять более 1 токена одному человеку раз в 8 часов");

        var eligiblePositions = _portfolio
            .Where(p => tokenPrices.TryGetValue(p.TokenSymbol, out var price) && price <= MaxTokenPrice && p.Quantity >= 1)
            .ToList();

        if (eligiblePositions.Count == 0)
            throw new DomainException("Нет подходящих токенов в портфеле (все токены дороже лимита или портфель пуст)");

        var random = Random.Shared;
        var position = eligiblePositions[random.Next(eligiblePositions.Count)];

        var giftId = Guid.NewGuid();
        var sentAt = timeProvider.GetUtcNow().UtcDateTime;
        var currentPrice = tokenPrices[position.TokenSymbol];

        position.RemoveQuantity(1);

        var sentGift = new SentGift(giftId, recipientId, position.TokenSymbol, 1, currentPrice, sentAt);
        _giftsSent.Add(sentGift);

        await PublishAsync(new GiftSentEvent(giftId, Id, recipientId, position.TokenSymbol, 1, sentAt));

        if (position.Quantity == 0)
            _portfolio.Remove(position);
    }

    public async Task ReceiveGift(
        Guid giftId,
        long senderId,
        string tokenSymbol,
        decimal quantity,
        TimeProvider timeProvider)
    {
        var receivedGift = _giftsReceived.FirstOrDefault(g =>
            g.GiftId == giftId && g.Status == GiftStatus.Sent);
        if (receivedGift is null)
            throw new DomainException("Gift not found or already received");

        receivedGift.MarkAsReceived(timeProvider.GetUtcNow().UtcDateTime);

        var existing = _portfolio.FirstOrDefault(p => p.TokenSymbol == tokenSymbol);
        if (existing is null)
        {
            _portfolio.Add(new PortfolioPosition(tokenSymbol, quantity));
        }
        else
        {
            existing.AddQuantity(quantity);
        }

        await PublishAsync(new GiftReceivedEvent(
            giftId, senderId, Id, tokenSymbol, quantity,
            timeProvider.GetUtcNow().UtcDateTime));
    }

    public async Task ReceiveAllGifts(TimeProvider timeProvider)
    {
        var pendingGifts = _giftsReceived
            .Where(g => g.Status == GiftStatus.Sent)
            .ToList();

        if (pendingGifts.Count == 0)
            throw new DomainException("No pending gifts to receive");

        var receivedAt = timeProvider.GetUtcNow().UtcDateTime;
        var receivedData = new List<GiftReceivedData>();

        foreach (var gift in pendingGifts)
        {
            gift.MarkAsReceived(receivedAt);

            var existing = _portfolio.FirstOrDefault(p => p.TokenSymbol == gift.TokenSymbol);
            if (existing is null)
            {
                _portfolio.Add(new PortfolioPosition(gift.TokenSymbol, gift.Quantity));
            }
            else
            {
                existing.AddQuantity(gift.Quantity);
            }

            receivedData.Add(new GiftReceivedData(
                gift.GiftId, gift.SenderId, gift.TokenSymbol, gift.Quantity));
        }

        await PublishAsync(new AllGiftsReceivedEvent(Id, receivedData, receivedAt));
    }

    internal void AttachPortfolio(PortfolioPosition position) => _portfolio.Add(position);

    internal void AttachSentGift(SentGift gift) => _giftsSent.Add(gift);

    internal void AttachReceivedGift(ReceivedGift gift) => _giftsReceived.Add(gift);
}

internal class PortfolioPosition
{
    public string TokenSymbol { get; }
    public decimal Quantity { get; private set; }

    internal PortfolioPosition(string tokenSymbol, decimal quantity)
    {
        TokenSymbol = tokenSymbol;
        Quantity = quantity;
    }

    internal void RemoveQuantity(decimal amount)
    {
        if (amount > Quantity)
            throw new DomainException("Insufficient quantity");
        Quantity -= amount;
    }

    internal void AddQuantity(decimal amount)
    {
        Quantity += amount;
    }
}

internal class SentGift
{
    public Guid GiftId { get; }
    public long RecipientId { get; }
    public string TokenSymbol { get; }
    public decimal Quantity { get; }
    public decimal PriceAtSend { get; }
    public GiftStatus Status { get; private set; }
    public DateTime SentAt { get; }

    internal SentGift(Guid giftId, long recipientId, string tokenSymbol, decimal quantity, decimal priceAtSend, DateTime sentAt)
    {
        GiftId = giftId;
        RecipientId = recipientId;
        TokenSymbol = tokenSymbol;
        Quantity = quantity;
        PriceAtSend = priceAtSend;
        Status = GiftStatus.Sent;
        SentAt = sentAt;
    }

    internal void MarkAsReceived() => Status = GiftStatus.Received;
}

internal class ReceivedGift
{
    public Guid GiftId { get; }
    public long SenderId { get; }
    public string TokenSymbol { get; }
    public decimal Quantity { get; }
    public GiftStatus Status { get; private set; }
    public DateTime SentAt { get; }
    public DateTime? ReceivedAt { get; private set; }

    internal ReceivedGift(Guid giftId, long senderId, string tokenSymbol, decimal quantity, DateTime sentAt)
    {
        GiftId = giftId;
        SenderId = senderId;
        TokenSymbol = tokenSymbol;
        Quantity = quantity;
        Status = GiftStatus.Sent;
        SentAt = sentAt;
    }

    internal void MarkAsReceived(DateTime receivedAt)
    {
        Status = GiftStatus.Received;
        ReceivedAt = receivedAt;
    }
}

internal enum GiftStatus
{
    Sent,
    Received
}
