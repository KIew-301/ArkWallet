using ArkWallet.Domain.Common;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Domain.GiftContext;
using Moq;

namespace ArkWallet.Tests.DomainTests.Gift;

public class UserSendGiftTest
{
    private static readonly DateTime Now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static User BuildUser(
        long id = 1001,
        string name = "Sender",
        List<Tokens>? portfolio = null,
        List<SentGift>? sentGifts = null,
        IEventPublisher? publisher = null)
    {
        var user = User.Load(
            id,
            name,
            portfolio ?? new List<Tokens>(),
            sentGifts ?? new List<SentGift>());
        user.SetEventPublisher(publisher ?? new Mock<IEventPublisher>().Object);
        return user;
    }

    private static Tokens Token(string symbol, int quantity, decimal price)
        => new Tokens(symbol, quantity, price);

    [Fact]
    public async Task SendGift_SuccessfulSend_RaisesCorrectGiftSentEvent()
    {
        var publisher = new Mock<IEventPublisher>();
        var user = BuildUser(publisher: publisher.Object, portfolio: new List<Tokens>
        {
            Token("AAA", 3, 500m)
        });

        var giftSent = await user.SendGift(2002, Now);

        Assert.Equal(1001, giftSent.SenderId);
        Assert.Equal(2002, giftSent.RecipientId);
        Assert.Equal("Sender", giftSent.SenderName);
        Assert.Equal("AAA", giftSent.Symbol);
        Assert.Equal(1, giftSent.Quantity);
        Assert.Equal(Now, giftSent.CreatedAt);
        publisher.Verify(p => p.PublishAsync(It.Is<GiftSentEvent>(e =>
            e.SenderId == 1001 && e.RecipientId == 2002 && e.Quantity == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendGift_SuccessfulSend_RemovesOneTokenFromPortfolio()
    {
        var user = BuildUser(portfolio: new List<Tokens>
        {
            Token("AAA", 5, 500m)
        });

        await user.SendGift(2002, Now);

        var token = user.Portfolio.Single();
        Assert.Equal(4, token.Quantity);
        Assert.False(token.IsEmpty);
    }

    [Fact]
    public async Task SendGift_TokenQuantityWasOne_PositionIsRemovedFromPortfolio()
    {
        var user = BuildUser(portfolio: new List<Tokens>
        {
            Token("AAA", 1, 500m)
        });

        await user.SendGift(2002, Now);

        Assert.Empty(user.Portfolio);
    }

    [Fact]
    public async Task SendGift_CooldownIsPerRecipient_OtherRecipientGiftDoesNotBlock()
    {
        var user = BuildUser(
            sentGifts: new List<SentGift> { new SentGift(3003, Now.AddHours(-1)) },
            portfolio: new List<Tokens>
            {
                Token("AAA", 5, 500m)
            });

        var giftSent = await user.SendGift(2002, Now);
        var giftSent2 = await user.SendGift(4004, Now);

        Assert.Equal("AAA", giftSent.Symbol);
        Assert.Equal("AAA", giftSent2.Symbol);
        Assert.Equal(3, user.Portfolio.Single().Quantity);
    }

    [Fact]
    public async Task SendGift_RandomSelection_SelectedTokenBelongsToEligibleSet()
    {
        var eligibleA = Token("AAA", 1000, 100m);
        var eligibleB = Token("BBB", 1000, 999m);
        var notEligible = Token("CCC", 1000, 5000m);
        var user = BuildUser(portfolio: new List<Tokens>
        {
            eligibleA,
            eligibleB,
            notEligible
        });

        for (var i = 0; i < 20; i++)
        {
            var giftSent = await user.SendGift(2002 + i, Now.AddMinutes(i));
            Assert.Contains(giftSent.Symbol, new[] { "AAA", "BBB" });
        }
    }

    [Fact]
    public async Task SendGift_CooldownSameRecipient_Throws()
    {
        var user = BuildUser(
            publisher: new Mock<IEventPublisher>().Object,
            sentGifts: new List<SentGift> { new SentGift(2002, Now.AddHours(-1)) },
            portfolio: new List<Tokens>
            {
                Token("AAA", 10, 500m)
            });

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => user.SendGift(2002, Now));

        Assert.Equal("Нельзя отправлять более 1 токена одному человеку раз в 8 часов", ex.Message);
    }

    [Fact]
    public async Task SendGift_ExactlyEightHoursAgo_IsAllowed()
    {
        var user = BuildUser(
            sentGifts: new List<SentGift> { new SentGift(2002, Now.AddHours(-8)) },
            portfolio: new List<Tokens>
            {
                Token("AAA", 10, 500m)
            });

        var giftSent = await user.SendGift(2002, Now);

        Assert.Equal(9, user.Portfolio.Single().Quantity);
        Assert.Equal("AAA", giftSent.Symbol);
    }

    [Fact]
    public async Task SendGift_SelfGift_ThrowsAndPortfolioUntouched()
    {
        var token = Token("AAA", 5, 500m);
        var user = BuildUser(publisher: new Mock<IEventPublisher>().Object, portfolio: new List<Tokens> { token });

        var ex = await Assert.ThrowsAsync<DomainException>(() => user.SendGift(1001, Now));

        Assert.Equal("Нельзя отправить подарок самому себе", ex.Message);
        Assert.Equal(5, token.Quantity);
    }

    [Fact]
    public async Task SendGift_NoEligibleTokens_ThrowsAndPortfolioUnchanged()
    {
        var token = Token("AAA", 5, 5000m);
        var user = BuildUser(publisher: new Mock<IEventPublisher>().Object, portfolio: new List<Tokens> { token });

        var ex = await Assert.ThrowsAsync<DomainException>(() => user.SendGift(2002, Now));

        Assert.Equal("Нет подходящих токенов в портфеле (все токены дороже лимита или портфель пуст)", ex.Message);
        Assert.Equal(5, token.Quantity);
    }

    [Fact]
    public async Task SendGift_EmptyPortfolio_Throws()
    {
        var user = BuildUser(publisher: new Mock<IEventPublisher>().Object);

        var ex = await Assert.ThrowsAsync<DomainException>(() => user.SendGift(2002, Now));

        Assert.Equal("Нет подходящих токенов в портфеле (все токены дороже лимита или портфель пуст)", ex.Message);
    }
}
