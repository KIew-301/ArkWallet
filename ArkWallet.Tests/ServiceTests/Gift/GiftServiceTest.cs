using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.GiftServices;
using ArkWallet.Application.Services.GiftServices;
using ArkWallet.Domain.Common;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.ServiceTests.Gift;

public class GiftServiceTest
{
    private static readonly string[] ReturnableTokenSymbols = new[] { "AAA", "BBB" };

    private readonly TestTimeProvider _time = new();
    private readonly Mock<IEventPublisher> _eventPublisher = new();

    private (GiftSendingService sending, Mock<ITokenQueryService> tokenQuery) CreateSendingService(ArkWalletDbContext db)
    {
        var tokenQuery = new Mock<ITokenQueryService>();
        var service = new GiftSendingService(db, tokenQuery.Object, _eventPublisher.Object, NullLogger<GiftSendingService>.Instance, _time);
        return (service, tokenQuery);
    }

    private GiftReceivingService CreateReceivingService(ArkWalletDbContext db)
    {
        return new GiftReceivingService(db, _eventPublisher.Object, NullLogger<GiftReceivingService>.Instance, _time);
    }

    private static void SetupAllTokens(Mock<ITokenQueryService> tokenQuery, params (string Symbol, decimal Price)[] tokens)
    {
        var tokenInfos = tokens
            .Select(t => new TokenInfo(t.Symbol, t.Symbol, t.Price, "icon.zzz", "img.zzz"))
            .ToList();
        tokenQuery
            .Setup(x => x.GetAllActiveTokensAsync())
            .ReturnsAsync(Result<List<TokenInfoWithPriceChange>>.Ok(
                tokenInfos.Select(t => new TokenInfoWithPriceChange(t, 0)).ToList()));
    }

    [Fact]
    public async Task SendGift_PortfolioEmptyAfterSend_PortfolioItemRemoved()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 1001, "ZZZ", 1);

        var (sending, tokenQuery) = CreateSendingService(db);
        SetupAllTokens(tokenQuery, ("ZZZ", 50));

        var result = await sending.SendGiftAsync(1001, 1002);

        Assert.True(result.IsSuccess, $"Error: {result.Message}");

        var portfolio = await db.PortfolioItems
            .FirstOrDefaultAsync(p => p.TraderTelegramId == 1001 && p.CharacterTokenId == "ZZZ");

        Assert.Null(portfolio);

        var gift = await db.Gifts.SingleAsync();
        Assert.Equal(1001, gift.SenderId);
        Assert.Equal(1002, gift.RecipientId);
        Assert.Equal("ZZZ", gift.TokenSymbol);
        Assert.Equal(1, gift.Quantity);
        Assert.Equal("Sent", gift.Status);
    }

    [Fact]
    public async Task SendGift_SamePersonWithin8Hours_Fails()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 1001, "ZZZ", 10);

        var (sending, tokenQuery) = CreateSendingService(db);
        SetupAllTokens(tokenQuery, ("ZZZ", 50));

        var result1 = await sending.SendGiftAsync(1001, 1002);
        Assert.True(result1.IsSuccess);

        _time.SkipInSeconds(60);

        var result2 = await sending.SendGiftAsync(1001, 1002);
        Assert.False(result2.IsSuccess, $"Expected failure but got: {result2.Message}");
    }

    [Fact]
    public async Task SendGift_DifferentPersonWithin8Hours_Succeeds()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.RegisterTrader(db, 1003);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 1001, "ZZZ", 10);

        var (sending, tokenQuery) = CreateSendingService(db);
        SetupAllTokens(tokenQuery, ("ZZZ", 50));

        var result1 = await sending.SendGiftAsync(1001, 1002);
        Assert.True(result1.IsSuccess);

        _time.SkipInSeconds(60);

        var result2 = await sending.SendGiftAsync(1001, 1003);
        Assert.True(result2.IsSuccess);
    }

    [Fact]
    public async Task SendGift_AllTokensAbovePriceLimit_Fails()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 1001, "ZZZ", 5);

        var (sending, tokenQuery) = CreateSendingService(db);
        SetupAllTokens(tokenQuery, ("ZZZ", 1500));

        var result = await sending.SendGiftAsync(1001, 1002);
        Assert.False(result.IsSuccess);
        Assert.Contains("подходящих токенов", result.Message);
    }

    [Fact]
    public async Task SendGift_TokensRemovedFromSender()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 1001, "ZZZ", 10);

        var (sending, tokenQuery) = CreateSendingService(db);
        SetupAllTokens(tokenQuery, ("ZZZ", 50));

        var result = await sending.SendGiftAsync(1001, 1002);
        Assert.True(result.IsSuccess);

        var senderPortfolio = await db.PortfolioItems
            .FirstOrDefaultAsync(p => p.TraderTelegramId == 1001 && p.CharacterTokenId == "ZZZ");
        Assert.NotNull(senderPortfolio);
        Assert.Equal(9, senderPortfolio.Quantity);
    }

    [Fact]
    public async Task SendGift_RandomTokenFromMultipleChoices()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "AAA");
        await HelpMethods.CreateToken(db, "BBB");
        await HelpMethods.AddPortfolio(db, 1001, "AAA", 5);
        await HelpMethods.AddPortfolio(db, 1001, "BBB", 5);

        var (sending, tokenQuery) = CreateSendingService(db);
        SetupAllTokens(tokenQuery, ("AAA", 100), ("BBB", 200));

        var result = await sending.SendGiftAsync(1001, 1002);
        Assert.True(result.IsSuccess);

        result.TryGetData(out var data);
        Assert.Contains(data.TokenSymbol, ReturnableTokenSymbols);
        Assert.Equal(1, data.Quantity);
    }

    [Fact]
    public async Task SendGift_OnlyCheapTokensAvailable()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "CHEAP");
        await HelpMethods.CreateToken(db, "EXPENSIVE");
        await HelpMethods.AddPortfolio(db, 1001, "CHEAP", 5);
        await HelpMethods.AddPortfolio(db, 1001, "EXPENSIVE", 5);

        var (sending, tokenQuery) = CreateSendingService(db);
        SetupAllTokens(tokenQuery, ("CHEAP", 100), ("EXPENSIVE", 1500));

        var result = await sending.SendGiftAsync(1001, 1002);
        Assert.True(result.IsSuccess);

        result.TryGetData(out var data);
        Assert.Equal("CHEAP", data.TokenSymbol);
    }

    [Fact]
    public async Task ReceiveGift_TokensAddedToRecipient()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 1001, "ZZZ", 10);

        var (sending, tokenQuery) = CreateSendingService(db);
        SetupAllTokens(tokenQuery, ("ZZZ", 50));

        var sendResult = await sending.SendGiftAsync(1001, 1002);
        Assert.True(sendResult.IsSuccess);

        sendResult.TryGetData(out var sendData);
        var giftId = sendData.GiftId;

        var receiving = CreateReceivingService(db);
        var receiveResult = await receiving.ReceiveGiftAsync(1002, giftId);
        Assert.True(receiveResult.IsSuccess);

        var recipientPortfolio = await db.PortfolioItems
            .FirstOrDefaultAsync(p => p.TraderTelegramId == 1002 && p.CharacterTokenId == "ZZZ");
        Assert.NotNull(recipientPortfolio);
        Assert.Equal(1, recipientPortfolio.Quantity);
    }

    [Fact]
    public async Task ReceiveGift_GiftStatusChangesToReceived()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 1001, "ZZZ", 10);

        var (sending, tokenQuery) = CreateSendingService(db);
        SetupAllTokens(tokenQuery, ("ZZZ", 50));

        var sendResult = await sending.SendGiftAsync(1001, 1002);
        sendResult.TryGetData(out var sendData);
        var giftId = sendData.GiftId;

        var giftBefore = await db.Gifts.FindAsync(giftId);
        Assert.NotNull(giftBefore);
        Assert.Equal("Sent", giftBefore.Status);

        var receiving = CreateReceivingService(db);
        await receiving.ReceiveGiftAsync(1002, giftId);

        var giftAfter = await db.Gifts.FindAsync(giftId);
        Assert.NotNull(giftAfter);
        Assert.Equal("Received", giftAfter.Status);
        Assert.NotNull(giftAfter.ReceivedAt);
    }

    [Fact]
    public async Task ReceiveGift_Twice_Fails()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 1001, "ZZZ", 10);

        var (sending, tokenQuery) = CreateSendingService(db);
        SetupAllTokens(tokenQuery, ("ZZZ", 50));

        var sendResult = await sending.SendGiftAsync(1001, 1002);
        sendResult.TryGetData(out var sendData);
        var giftId = sendData.GiftId;

        var receiving = CreateReceivingService(db);
        var result1 = await receiving.ReceiveGiftAsync(1002, giftId);
        Assert.True(result1.IsSuccess);

        var result2 = await receiving.ReceiveGiftAsync(1002, giftId);
        Assert.False(result2.IsSuccess);
    }

    [Fact]
    public async Task SendGift_NoTokensInPortfolio_Fails()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ZZZ");

        var (sending, tokenQuery) = CreateSendingService(db);
        SetupAllTokens(tokenQuery, ("ZZZ", 50));

        var result = await sending.SendGiftAsync(1001, 1002);
        Assert.False(result.IsSuccess);
        Assert.Contains("подходящих токенов", result.Message);
    }

    [Fact]
    public async Task SendGift_ToSelf_Fails()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 1001, "ZZZ", 10);

        var (sending, tokenQuery) = CreateSendingService(db);
        SetupAllTokens(tokenQuery, ("ZZZ", 50));

        var result = await sending.SendGiftAsync(1001, 1001);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task SendGift_RecipientNotRegistered_Fails()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 1001, "ZZZ", 10);

        var (sending, tokenQuery) = CreateSendingService(db);
        SetupAllTokens(tokenQuery, ("ZZZ", 50));

        var result = await sending.SendGiftAsync(1001, 9999);
        Assert.False(result.IsSuccess);
    }
}
