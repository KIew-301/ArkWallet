using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Services.GiftServices;
using ArkWallet.Domain.Common;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.ServiceTests.Gift;

public class GiftSendingServiceTest
{
    private readonly TestTimeProvider _timeProvider = new();

    private static ArkWalletDbContext CreateDb() => DbTest.CreateInitializedDbContextAsync().GetAwaiter().GetResult();

    private static ITokenQueryService MockTokens(params (string Symbol, decimal Price)[] tokens)
    {
        var mock = new Mock<ITokenQueryService>();
        mock.Setup(t => t.GetAllActiveTokensAsync())
            .ReturnsAsync(Result<List<TokenInfoWithPriceChange>>.Ok(
                tokens.Select(t => new TokenInfoWithPriceChange(
                    new TokenInfo(t.Symbol, t.Symbol, t.Price, "icon.zzz", "img.zzz"),
                    0)).ToList()));
        return mock.Object;
    }

    private static GiftSendingService BuildService(
        ArkWalletDbContext db,
        ITokenQueryService tokenQueryService,
        IEventPublisher? publisher = null,
        TimeProvider? timeProvider = null)
    {
        var candle = new Mock<ITokenPriceCandleUpdateService>();
        candle
            .Setup(c => c.UpdateTokenPriceCandleAsync(It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync(Result.Ok());

        publisher ??= new MediatREventPublisher(TestMediatorFactory.Create(db, candle.Object));

        return new GiftSendingService(
            db,
            tokenQueryService,
            publisher,
            NullLogger<GiftSendingService>.Instance,
            timeProvider ?? new TestTimeProvider());
    }

    [Fact]
    public async Task SendGiftAsync_SenderDoesNotExist_ReturnsFail()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 2002);

        var service = BuildService(db, MockTokens(("AAA", 500m)));

        var result = await service.SendGiftAsync(1001, 2002);

        Assert.False(result.IsSuccess);
        Assert.Equal("Отправитель не найден", result.Message);
    }

    [Fact]
    public async Task SendGiftAsync_RecipientDoesNotExist_ReturnsFail()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 1001);

        var service = BuildService(db, MockTokens(("AAA", 500m)));

        var result = await service.SendGiftAsync(1001, 2002);

        Assert.False(result.IsSuccess);
        Assert.Equal("Получатель не найден", result.Message);
    }

    [Fact]
    public async Task SendGiftAsync_NoEligibleTokens_ReturnsFailAndPortfolioUntouched()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 2002);
        await HelpMethods.CreateToken(db, "AAA", price: 5000m);
        await HelpMethods.AddPortfolio(db, 1001, "AAA", 5);

        var service = BuildService(db, MockTokens(("AAA", 5000m)));

        var result = await service.SendGiftAsync(1001, 2002);

        Assert.False(result.IsSuccess);
        Assert.Equal("Нет подходящих токенов в портфеле (все токены дороже лимита или портфель пуст)", result.Message);
        var portfolio = await HelpMethods.GetPortfolio(db, 1001, "AAA");
        Assert.Equal(5, portfolio.Quantity);
    }

    [Fact]
    public async Task SendGiftAsync_SenderHasNoPortfolio_ReturnsFail()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 2002);

        var service = BuildService(db, MockTokens(("AAA", 500m)));

        var result = await service.SendGiftAsync(1001, 2002);

        Assert.False(result.IsSuccess);
        Assert.Equal("Нет подходящих токенов в портфеле (все токены дороже лимита или портфель пуст)", result.Message);
    }

    [Fact]
    public async Task SendGiftAsync_HappyPath_DecreasesPortfolioQuantityByOne()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 2002);
        await HelpMethods.CreateToken(db, "AAA", price: 500m);
        await HelpMethods.AddPortfolio(db, 1001, "AAA", 5);

        var service = BuildService(db, MockTokens(("AAA", 500m)));

        var result = await service.SendGiftAsync(1001, 2002);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal("AAA", data.TokenSymbol);
        Assert.Equal(1, data.Quantity);
        var portfolio = await HelpMethods.GetPortfolio(db, 1001, "AAA");
        Assert.Equal(4, portfolio.Quantity);
    }

    [Fact]
    public async Task SendGiftAsync_HappyPath_QuantityOne_RemovesPosition()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 2002);
        await HelpMethods.CreateToken(db, "AAA", price: 500m);
        await HelpMethods.AddPortfolio(db, 1001, "AAA", 1);

        var service = BuildService(db, MockTokens(("AAA", 500m)));

        var result = await service.SendGiftAsync(1001, 2002);

        Assert.True(result.IsSuccess);
        Assert.False(await db.PortfolioItems.AnyAsync(p => p.TraderTelegramId == 1001 && p.CharacterTokenId == "AAA"));
    }

    [Fact]
    public async Task SendGiftAsync_HappyPath_CreatesGiftMailMessage()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 1001, "SenderName");
        await HelpMethods.RegisterTrader(db, 2002);
        await HelpMethods.CreateToken(db, "AAA", price: 500m);
        await HelpMethods.AddPortfolio(db, 1001, "AAA", 3);

        var service = BuildService(db, MockTokens(("AAA", 500m)));

        var result = await service.SendGiftAsync(1001, 2002);

        Assert.True(result.IsSuccess);
        var mail = await db.MailMessages.SingleAsync(m => m.Type == MailType.Gift.ToString());
        Assert.Equal(2002, mail.TraderId);
        Assert.Equal(1001, mail.SenderId);
        Assert.Equal("AAA", mail.SymbolForReward);
        Assert.Equal(1, mail.AmountForReward);
    }

    [Fact]
    public async Task SendGiftAsync_CooldownSameRecipient_ReturnsError()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 2002);
        await HelpMethods.CreateToken(db, "AAA", price: 500m);
        await HelpMethods.AddPortfolio(db, 1001, "AAA", 5);

        var service = BuildService(db, MockTokens(("AAA", 500m)), timeProvider: _timeProvider);

        var first = await service.SendGiftAsync(1001, 2002);
        Assert.True(first.IsSuccess);

        _timeProvider.SkipInSeconds(7 * 3600);
        var second = await service.SendGiftAsync(1001, 2002);

        Assert.False(second.IsSuccess);
        Assert.Equal("Нельзя отправлять более 1 токена одному человеку раз в 8 часов", second.Message);

        var portfolio = await HelpMethods.GetPortfolio(db, 1001, "AAA");
        Assert.Equal(4, portfolio.Quantity);
    }

    [Fact]
    public async Task SendGiftAsync_TokenPriceComesFromTokenQueryService()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 2002);
        await HelpMethods.CreateToken(db, "AAA", price: 500m);
        await HelpMethods.AddPortfolio(db, 1001, "AAA", 2);

        var service = BuildService(db, MockTokens(("AAA", 5000m)));

        var result = await service.SendGiftAsync(1001, 2002);

        Assert.False(result.IsSuccess);
        Assert.Equal("Нет подходящих токенов в портфеле (все токены дороже лимита или портфель пуст)", result.Message);
        var portfolio = await HelpMethods.GetPortfolio(db, 1001, "AAA");
        Assert.Equal(2, portfolio.Quantity);
    }

    [Fact]
    public async Task SendGiftAsync_SenderWithoutUsername_UsesFallbackName()
    {
        using var db = CreateDb();
        var trader = global::ArkWallet.Domain.Entities.Trader.Create(1001, null);
        db.Traders.Add(trader);
        await db.SaveChangesAsync();
        await HelpMethods.RegisterTrader(db, 2002);
        await HelpMethods.CreateToken(db, "AAA", price: 500m);
        await HelpMethods.AddPortfolio(db, 1001, "AAA", 3);

        var service = BuildService(db, MockTokens(("AAA", 500m)));

        var result = await service.SendGiftAsync(1001, 2002);

        Assert.True(result.IsSuccess);
        var mail = await db.MailMessages.SingleAsync(m => m.Type == MailType.Gift.ToString());
        Assert.Equal("ID 1001", mail.SenderName);
    }

    [Fact]
    public async Task SendGiftAsync_HandlerThrows_RollsBackPortfolioAndMail()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 2002);
        await HelpMethods.CreateToken(db, "AAA", price: 500m);
        await HelpMethods.AddPortfolio(db, 1001, "AAA", 3);

        var throwingPublisher = new Mock<IEventPublisher>();
        throwingPublisher
            .Setup(p => p.PublishAsync(It.IsAny<MediatR.INotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Handler failure"));

        var service = BuildService(db, MockTokens(("AAA", 500m)), publisher: throwingPublisher.Object);

        var result = await service.SendGiftAsync(1001, 2002);

        Assert.False(result.IsSuccess);
        var portfolio = await HelpMethods.GetPortfolio(db, 1001, "AAA");
        Assert.Equal(3, portfolio.Quantity);
        Assert.False(await db.MailMessages.AnyAsync(m => m.Type == MailType.Gift.ToString()));
    }
}
