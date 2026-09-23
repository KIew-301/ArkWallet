using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices;
using ArkWallet.Core.SubscriptionContext.Application.Dtos;
using ArkWallet.Infrastructure.Wizard;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ArkWallet.Tests.IntegrationTests;

public class WizardSubscriptionCommandsTest
{
    private readonly ServiceMocks _m;
    private readonly WizardEngine _engine;

    private const long UserId = 2100;

    public WizardSubscriptionCommandsTest()
    {
        _m = WizardEngineTestHelper.Build();
        _engine = _m.Engine;
    }

    private static List<SubscriptionOfferInfo> Offers() => new()
    {
        new(
            Id: 1,
            Name: "Базовая",
            Level: 1,
            PriceRubles: 0,
            PriceWeekRubles: 0,
            PriceMonthRubles: 120,
            PriceYearRubles: 0,
            MaxOrders: 5,
            MaxMiningMachines: 5,
            DurationMinutes: null,
            Action: SubscriptionOfferAction.Buy,
            BonusMinutes: null),
        new(
            Id: 2,
            Name: "Премиум",
            Level: 2,
            PriceRubles: 100,
            PriceWeekRubles: 30,
            PriceMonthRubles: 90,
            PriceYearRubles: 900,
            MaxOrders: 20,
            MaxMiningMachines: 20,
            DurationMinutes: 10080,
            Action: SubscriptionOfferAction.Upgrade,
            BonusMinutes: 1440),
    };

    private void SeedOffers()
    {
        _m.SubscriptionQuery
            .Setup(s => s.GetOffersForTraderAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<SubscriptionOfferInfo>>.Ok(Offers()));
    }

    // ═══════════════════════════════════════════════════════════
    //  /subscriptions → список + кнопки выбора подписки
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Subscriptions_ShowsListWithChoiceButtons()
    {
        SeedOffers();

        var result = await _engine.ProcessInput(UserId, "/subscriptions");

        Assert.NotNull(result.Message);
        Assert.Contains("Доступные подписки", result.Message);
        Assert.Contains("Базовая", result.Message);
        Assert.Contains("Премиум", result.Message);
        Assert.Contains("уровень 2", result.Message);

        Assert.NotNull(result.Buttons);
        Assert.Contains(result.Buttons!, b => b.Value == "sub_detail 1");
        Assert.Contains(result.Buttons!, b => b.Value == "sub_detail 2");
        Assert.Contains(result.Buttons!, b => b.Value == "/subscriptions");
    }

    [Fact]
    public async Task Subscriptions_NoOffers_ShowsUnavailableMessage()
    {
        _m.SubscriptionQuery
            .Setup(s => s.GetOffersForTraderAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<SubscriptionOfferInfo>>.Ok(new List<SubscriptionOfferInfo>()));

        var result = await _engine.ProcessInput(UserId, "/subscriptions");

        Assert.Equal("Подписки временно недоступны.", result.Message);
        Assert.Null(result.Buttons);
    }

    // ═══════════════════════════════════════════════════════════
    //  sub_detail <id> → описание + кнопки сроков
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task SubDetail_ShowsPeriodButtonsAndBackButton()
    {
        SeedOffers();

        var result = await _engine.ProcessInput(UserId, "sub_detail 2");

        Assert.NotNull(result.Message);
        Assert.Contains("Премиум", result.Message);
        Assert.Contains("уровень 2", result.Message);
        Assert.Contains("Макс. ордеров: 20", result.Message);
        Assert.Contains("Бонус за переход: +1440 мин.", result.Message);

        Assert.NotNull(result.Buttons);
        Assert.Contains(result.Buttons!, b => b.Value == "sub_buy 2 week");
        Assert.Contains(result.Buttons!, b => b.Value == "sub_buy 2 month");
        Assert.Contains(result.Buttons!, b => b.Value == "sub_buy 2 year");
        Assert.Contains(result.Buttons!, b => b.Value == "/subscriptions");
    }

    [Fact]
    public async Task SubDetail_ZeroPricePeriods_HiddenFromButtons()
    {
        SeedOffers();

        var result = await _engine.ProcessInput(UserId, "sub_detail 1");

        Assert.NotNull(result.Buttons);
        Assert.Contains(result.Buttons!, b => b.Value == "sub_buy 1 month");
        Assert.DoesNotContain(result.Buttons!, b => b.Value == "sub_buy 1 week");
        Assert.DoesNotContain(result.Buttons!, b => b.Value == "sub_buy 1 year");
    }

    [Fact]
    public async Task SubDetail_UnknownSubscription_ReturnsError()
    {
        SeedOffers();

        var result = await _engine.ProcessInput(UserId, "sub_detail 99");

        Assert.Contains("Подписка не найдена", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  sub_buy <id> <период> → стоимость + ссылка на оплату
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task SubBuy_RequiresConfirmation_ShowsCostAndPaymentLink()
    {
        SeedOffers();
        _m.PurchaseService
            .Setup(p => p.PurchaseAsync(UserId, 2, SubscriptionPeriod.Month, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseResult(true, "Счёт создан", "PAY-1", null)
            {
                RequiresConfirmation = true,
                ConfirmationUrl = "https://yookassa.ru/pay/abc"
            });

        var result = await _engine.ProcessInput(UserId, "sub_buy 2 month");

        Assert.NotNull(result.Message);
        Assert.NotNull(result.Buttons);
        Assert.Contains("Счёт на оплату создан!", result.Message);
        Assert.Contains("Итоговая стоимость: 90,00 ₽", result.Message);
        Assert.Contains("https://yookassa.ru/pay/abc", result.Message);
        Assert.Contains(result.Buttons!, b => b.Value == "/subscriptions");
    }

    [Fact]
    public async Task SubBuy_WithoutConfirmation_ShowsSuccess()
    {
        SeedOffers();
        _m.PurchaseService
            .Setup(p => p.PurchaseAsync(UserId, 2, SubscriptionPeriod.Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseResult(true, "Подписка приобретена", "PAY-2", new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        var result = await _engine.ProcessInput(UserId, "sub_buy 2 year");

        Assert.NotNull(result.Message);
        Assert.Contains("Подписка оформлена на год!", result.Message);
        Assert.Contains("Итоговая стоимость: 900,00 ₽", result.Message);
        Assert.Contains("Транзакция: PAY-2", result.Message);
        Assert.NotNull(result.Buttons);
        Assert.Contains(result.Buttons!, b => b.Value == "/subscriptions");
    }

    [Fact]
    public async Task SubBuy_PurchaseFailure_ShowsErrorAndBackButton()
    {
        SeedOffers();
        _m.PurchaseService
            .Setup(p => p.PurchaseAsync(UserId, 2, SubscriptionPeriod.Week, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PurchaseResult.Fail("Недостаточно средств"));

        var result = await _engine.ProcessInput(UserId, "sub_buy 2 week");

        Assert.NotNull(result.Message);
        Assert.Contains("Не удалось купить подписку", result.Message);
        Assert.NotNull(result.Buttons);
        Assert.Contains(result.Buttons!, b => b.Value == "/subscriptions");
    }

    // ═══════════════════════════════════════════════════════════
    //  /admin_update_subscription — обновление данных подписки
    // ═══════════════════════════════════════════════════════════

    private const long AdminUserId = 999999;

    [Fact]
    public async Task AdminUpdateSubscription_ShowsJsonPrompt()
    {
        var result = await _engine.ProcessInput(AdminUserId, "/admin_update_subscription");

        Assert.NotNull(result.Message);
        Assert.Contains("subscriptionId", result.Message);
        Assert.Contains("name", result.Message);
    }

    [Fact]
    public async Task AdminUpdateSubscription_NonAdmin_Rejects()
    {
        var result = await _engine.ProcessInput(UserId, "/admin_update_subscription");
        var prompt = await _engine.ProcessInput(UserId, """{"subscriptionId": 2, "name": "X"}""");

        Assert.NotNull(prompt.Message);
        Assert.Contains("Admin_Main only", prompt.Message);
    }

    [Fact]
    public async Task AdminUpdateSubscription_MissingId_ReturnsError()
    {
        _ = await _engine.ProcessInput(AdminUserId, "/admin_update_subscription");
        var result = await _engine.ProcessInput(AdminUserId, """{"name": "X"}""");

        Assert.Contains("Required fields: subscriptionId", result.Message);
    }

    [Fact]
    public async Task AdminUpdateSubscription_UnknownSubscription_ReturnsError()
    {
        await _m.Db.Database.EnsureCreatedAsync();
        _ = await _engine.ProcessInput(AdminUserId, "/admin_update_subscription");
        var result = await _engine.ProcessInput(AdminUserId, """{"subscriptionId": 999}""");

        Assert.Contains("not found", result.Message);
    }

    [Fact]
    public async Task AdminUpdateSubscription_UpdatesNameAndDescription()
    {
        await _m.Db.Database.EnsureCreatedAsync();
        _m.Db.Subscriptions.Add(new ArkWallet.Infrastructure.Data.Subscription
        {
            Id = 2,
            Name = "Премиум",
            Level = 2,
            PriceWeekRubles = 30,
            PriceMonthRubles = 90,
            PriceYearRubles = 900,
            MaxOrders = 20,
            MaxMiningMachines = 20
        });
        await _m.Db.SaveChangesAsync();

        _ = await _engine.ProcessInput(AdminUserId, "/admin_update_subscription");
        var result = await _engine.ProcessInput(AdminUserId, """{"subscriptionId": 2, "name": "VIP", "description": "Описание VIP"}""");

        Assert.Contains("updated", result.Message);
        var updated = await _m.Db.Subscriptions.FirstOrDefaultAsync(s => s.Id == 2);
        Assert.NotNull(updated);
        Assert.Equal("VIP", updated!.Name);
        Assert.Equal("Описание VIP", updated.Description);
        Assert.Equal(43200, updated.DurationMinutes);
    }

    [Fact]
    public async Task AdminUpdateSubscription_LevelValidation_RequiresLowerLevel()
    {
        await _m.Db.Database.EnsureCreatedAsync();
        _m.Db.Subscriptions.Add(new ArkWallet.Infrastructure.Data.Subscription
        {
            Id = 1,
            Name = "Базовая",
            Level = 1,
            MaxOrders = 5,
            MaxMiningMachines = 5
        });
        _m.Db.Subscriptions.Add(new ArkWallet.Infrastructure.Data.Subscription
        {
            Id = 2,
            Name = "Премиум",
            Level = 2,
            MaxOrders = 20,
            MaxMiningMachines = 20
        });
        await _m.Db.SaveChangesAsync();

        _ = await _engine.ProcessInput(AdminUserId, "/admin_update_subscription");
        var result = await _engine.ProcessInput(AdminUserId, """{"subscriptionId": 2, "level": 4}""");

        Assert.Contains("required to exist first", result.Message);
    }
}