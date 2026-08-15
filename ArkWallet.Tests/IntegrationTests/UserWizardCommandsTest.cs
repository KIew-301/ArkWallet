using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Leaders;
using ArkWallet.Application.Contracts.MarketMaker;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Contracts.TradeServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Application.Services.Wizard;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Wizard;
using Moq;

namespace ArkWallet.Tests.IntegrationTests;

public class UserWizardCommandsTest : IDisposable
{
    private readonly ServiceMocks _m;
    private readonly WizardEngine _engine;

    private const long UserId = 1001;

    public UserWizardCommandsTest()
    {
        _m = WizardEngineTestHelper.Build();
        _engine = _m.Engine;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n");

    // ═══════════════════════════════════════════════════════════
    //  /start
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Start_NewUser_ShowsNameQuestion()
    {
        _m.TraderRegistration
            .Setup(s => s.CheckTraderAlreadyRegistered(UserId))
            .ReturnsAsync(false);

        var result = await _engine.ProcessInput(UserId, "/start");

        Assert.NotNull(result.Message);
        Assert.Equal("Как вас будут звать?", result.Message);
    }

    [Fact]
    public async Task Start_NewUser_ProceedsWithName_RegistersSuccessfully()
    {
        _m.TraderRegistration
            .Setup(s => s.CheckTraderAlreadyRegistered(UserId))
            .ReturnsAsync(false);
        _m.TraderRegistration
            .Setup(s => s.RegisterTraderAsync(UserId, "Alice", true))
            .ReturnsAsync(Result.Ok());

        await _engine.ProcessInput(UserId, "/start");
        var result = await _engine.ProcessInput(UserId, "Alice");

        Assert.NotNull(result.Message);
        Assert.Equal("Отлично! Вы успешно зарегистрированы!", result.Message);
    }

    [Fact]
    public async Task Start_AlreadyRegistered_ReturnsAlreadyMessage()
    {
        _m.TraderRegistration
            .Setup(s => s.CheckTraderAlreadyRegistered(UserId))
            .ReturnsAsync(true);

        var result = await _engine.ProcessInput(UserId, "/start");

        Assert.NotNull(result.Message);
        Assert.Equal(
            "Вы уже зарегистрированы! Используйте /get_profile для просмотра профиля.",
            result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  /get_profile
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetProfile_WithPortfolio_ShowsFullProfile()
    {
        _m.TraderQuery
            .Setup(s => s.GetTraderProfileAsync(UserId))
            .ReturnsAsync(Result<TraderProfileInfo>.Ok(new TraderProfileInfo("Alice", 5000m)));
        _m.BalanceSnapshot
            .Setup(s => s.TakeTotalTraderBalanceSnapshot(UserId))
            .ReturnsAsync(Result<BalanceSnapshotData>.Ok(
                new BalanceSnapshotData(UserId, 6281.84m, 5000m, 0, 0, 1281.84m, DateTime.UtcNow)));
        _m.PortfolioQuery
            .Setup(s => s.GetTraderTokensAsync(UserId))
            .ReturnsAsync(Result<PortfolioItemInfo[]>.Ok(new[]
            {
                new PortfolioItemInfo(20, 103.50m, 1949.64m, -5.81m,
                    new TokenInfo("LAPLD", "Lappland", 97.48m, "", ""))
            }));
        _m.LeadersTop
            .Setup(s => s.GetTraderPositionAsync(UserId))
            .ReturnsAsync(Result<LeaderPosition>.Ok(new LeaderPosition(2, 3, 6281.84m)));

        var result = await _engine.ProcessInput(UserId, "/get_profile");

        Assert.NotNull(result.Message);
        var msg = Normalize(result.Message);

        Assert.Contains("👤 Alice", msg);
        Assert.Contains("💰 Баланс: 5000,00₽", msg);
        Assert.Contains("📊 Общий баланс: 6281,84₽", msg);
        Assert.Contains("📦 Портфель:", msg);
        Assert.Contains("LAPLD: 20 шт. (куплено за 2070,00₽)", msg);
        Assert.Contains("Если продать сейчас", msg);
        Assert.Contains("🏆 Рейтинг по балансу: #2 из 3", msg);

        Assert.NotNull(result.Buttons);
        Assert.Single(result.Buttons);
        Assert.Equal("🔄 Обновить", result.Buttons[0].Text);
        Assert.Equal("/get_profile", result.Buttons[0].Value);
    }

    [Fact]
    public async Task GetProfile_EmptyPortfolio_ShowsEmptyMessage()
    {
        _m.TraderQuery
            .Setup(s => s.GetTraderProfileAsync(UserId))
            .ReturnsAsync(Result<TraderProfileInfo>.Ok(new TraderProfileInfo("Alice", 3000m)));
        _m.BalanceSnapshot
            .Setup(s => s.TakeTotalTraderBalanceSnapshot(UserId))
            .ReturnsAsync(Result<BalanceSnapshotData>.Ok(
                new BalanceSnapshotData(UserId, 3000m, 3000m, 0, 0, 0, DateTime.UtcNow)));
        _m.PortfolioQuery
            .Setup(s => s.GetTraderTokensAsync(UserId))
            .ReturnsAsync(Result<PortfolioItemInfo[]>.Ok(Array.Empty<PortfolioItemInfo>()));
        _m.LeadersTop
            .Setup(s => s.GetTraderPositionAsync(UserId))
            .ReturnsAsync(Result<LeaderPosition>.Ok(new LeaderPosition(1, 1, 3000m)));

        var result = await _engine.ProcessInput(UserId, "/get_profile");

        Assert.NotNull(result.Message);
        var msg = Normalize(result.Message);

        Assert.Contains("👤 Alice", msg);
        Assert.Contains("💰 Баланс: 3000,00₽", msg);
        Assert.Contains("Портфель:", msg);
        Assert.Contains("Пусто", msg);
        Assert.Contains("🏆 Рейтинг по балансу: #1 из 1", msg);
    }

    [Fact]
    public async Task GetProfile_UnregisteredTrader_ReturnsNotFoundError()
    {
        _m.TraderQuery
            .Setup(s => s.GetTraderProfileAsync(UserId))
            .ReturnsAsync(Result<TraderProfileInfo>.Fail("Trader not found."));

        var result = await _engine.ProcessInput(UserId, "/get_profile");

        Assert.NotNull(result.Message);
        Assert.Contains("Trader not found.", result.Message);
    }

    [Fact]
    public async Task GetProfile_SnapshotFails_UsesProfileBalanceAsTotal()
    {
        _m.TraderQuery
            .Setup(s => s.GetTraderProfileAsync(UserId))
            .ReturnsAsync(Result<TraderProfileInfo>.Ok(new TraderProfileInfo("Bob", 4000m)));
        _m.BalanceSnapshot
            .Setup(s => s.TakeTotalTraderBalanceSnapshot(UserId))
            .ReturnsAsync(Result<BalanceSnapshotData>.Fail("snapshot error"));
        _m.PortfolioQuery
            .Setup(s => s.GetTraderTokensAsync(UserId))
            .ReturnsAsync(Result<PortfolioItemInfo[]>.Ok(Array.Empty<PortfolioItemInfo>()));
        _m.LeadersTop
            .Setup(s => s.GetTraderPositionAsync(UserId))
            .ReturnsAsync(Result<LeaderPosition>.Fail("no position"));

        var result = await _engine.ProcessInput(UserId, "/get_profile");

        Assert.NotNull(result.Message);
        var msg = Normalize(result.Message);
        Assert.Contains("💰 Баланс: 4000,00₽", msg);
        Assert.Contains("📊 Общий баланс: 4000,00₽", msg);
        Assert.DoesNotContain("Рейтинг", msg);
    }

    // ═══════════════════════════════════════════════════════════
    //  /place_order — buy flow
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task PlaceOrder_ShowsDirectionQuestion()
    {
        var result = await _engine.ProcessInput(UserId, "/place_order");

        Assert.NotNull(result.Message);
        Assert.Equal("Вы желаете КУПИТЬ или ПРОДАТЬ токен?", result.Message);
        Assert.NotNull(result.Buttons);
        Assert.Equal(2, result.Buttons.Count);
        Assert.Equal("Купить", result.Buttons[0].Text);
        Assert.Equal("Продать", result.Buttons[1].Text);
    }

    [Fact]
    public async Task PlaceOrder_BuyFullFlow_CreatesOrder()
    {
        _m.OrderValidation
            .Setup(s => s.ValidateDirection("купить"))
            .Returns(new ValidationResult(true));
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("ZZZ"))
            .ReturnsAsync(Result<TokenInfo>.Ok(new TokenInfo("ZZZ", "Zero", 100m, "", "")));
        _m.OrderValidation
            .Setup(s => s.ValidateTokenAsync(UserId, "zzz", "купить"))
            .ReturnsAsync(new ValidationResult(true));
        _m.OrderValidation
            .Setup(s => s.ValidateQuantity(5))
            .Returns(new ValidationResult(true));
        _m.OrderValidation
            .Setup(s => s.ValidatePrice(100m))
            .Returns(new ValidationResult(true));
        _m.OrderValidation
            .Setup(s => s.ValidateOrderCreationAsync(UserId, "ZZZ", "купить", 5, 100m))
            .ReturnsAsync(new ValidationResult(true));

        var orderDto = new OrderDto("order-1", Domain.ValueObjects.OrderType.Buy, UserId, "ZZZ", 5, 100m,
            Domain.ValueObjects.OrderStatus.Active, DateTime.UtcNow);
        _m.OrderCreation
            .Setup(s => s.CreateOrderAsync(It.IsAny<CreateOrderCommand>()))
            .ReturnsAsync(Result<OrderCreationData>.Ok(new OrderCreationData(false, orderDto)));

        var r1 = await _engine.ProcessInput(UserId, "/place_order");
        Assert.Equal("Вы желаете КУПИТЬ или ПРОДАТЬ токен?", r1.Message);

        var r2 = await _engine.ProcessInput(UserId, "Купить");
        Assert.Equal("Какой токен вы хотите купить/продать? (выберите или напишите)", r2.Message);

        var r3 = await _engine.ProcessInput(UserId, "zzz");
        Assert.Equal("Сколько вы хотите купить/продать? (выберите или напишите)", r3.Message);

        var r4 = await _engine.ProcessInput(UserId, "5");
        Assert.Equal("По какой цене вы хотите исполнить ордер? (выберите или напишите свою)", r4.Message);

        var r5 = await _engine.ProcessInput(UserId, "100");
        Assert.NotNull(r5.Message);
        Assert.Contains("Ожидаем", r5.Message);
        Assert.Contains("5 шт. токенов ZZZ", r5.Message);
        Assert.Contains("100,00₽", r5.Message);
    }

    [Fact]
    public async Task PlaceOrder_SellFullFlow_CreatesSellOrder()
    {
        _m.OrderValidation
            .Setup(s => s.ValidateDirection("продать"))
            .Returns(new ValidationResult(true));
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("ZZZ"))
            .ReturnsAsync(Result<TokenInfo>.Ok(new TokenInfo("ZZZ", "Zero", 150m, "", "")));
        _m.OrderValidation
            .Setup(s => s.ValidateTokenAsync(UserId, "ZZZ", "продать"))
            .ReturnsAsync(new ValidationResult(true));
        _m.OrderValidation
            .Setup(s => s.ValidateQuantity(10))
            .Returns(new ValidationResult(true));
        _m.OrderValidation
            .Setup(s => s.ValidatePrice(150m))
            .Returns(new ValidationResult(true));
        _m.OrderValidation
            .Setup(s => s.ValidateOrderCreationAsync(UserId, "ZZZ", "продать", 10, 150m))
            .ReturnsAsync(new ValidationResult(true));

        var orderDto = new OrderDto("order-2", Domain.ValueObjects.OrderType.Sell, UserId, "ZZZ", 10, 150m,
            Domain.ValueObjects.OrderStatus.Active, DateTime.UtcNow);
        _m.OrderCreation
            .Setup(s => s.CreateOrderAsync(It.IsAny<CreateOrderCommand>()))
            .ReturnsAsync(Result<OrderCreationData>.Ok(new OrderCreationData(false, orderDto)));

        await _engine.ProcessInput(UserId, "/place_order");
        await _engine.ProcessInput(UserId, "Продать");
        await _engine.ProcessInput(UserId, "ZZZ");
        await _engine.ProcessInput(UserId, "10");
        var result = await _engine.ProcessInput(UserId, "150");

        Assert.NotNull(result.Message);
        Assert.Contains("Ожидаем", result.Message);
        Assert.Contains("когда у вас купят", result.Message);
        Assert.Contains("10 шт. токенов ZZZ", result.Message);
        Assert.Contains("150,00₽", result.Message);
    }

    [Fact]
    public async Task PlaceOrder_InvalidDirection_ShowsServerError()
    {
        _m.OrderValidation
            .Setup(s => s.ValidateDirection("invalid"))
            .Returns(new ValidationResult(false, "Неверное направление"));

        await _engine.ProcessInput(UserId, "/place_order");
        var result = await _engine.ProcessInput(UserId, "invalid");

        Assert.NotNull(result.Message);
        Assert.Contains("Ошибка", result.Message);
    }

    [Fact]
    public async Task PlaceOrder_InvalidQuantity_ShowsServerError()
    {
        _m.OrderValidation
            .Setup(s => s.ValidateDirection("купить"))
            .Returns(new ValidationResult(true));
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("ZZZ"))
            .ReturnsAsync(Result<TokenInfo>.Ok(new TokenInfo("ZZZ", "Zero", 100m, "", "")));
        _m.OrderValidation
            .Setup(s => s.ValidateTokenAsync(UserId, "zzz", "купить"))
            .ReturnsAsync(new ValidationResult(true));

        await _engine.ProcessInput(UserId, "/place_order");
        await _engine.ProcessInput(UserId, "Купить");
        await _engine.ProcessInput(UserId, "zzz");
        var result = await _engine.ProcessInput(UserId, "abc");

        Assert.NotNull(result.Message);
        Assert.Contains("Ошибка", result.Message);
    }

    [Fact]
    public async Task PlaceOrder_InvalidPrice_ShowsServerError()
    {
        _m.OrderValidation
            .Setup(s => s.ValidateDirection("купить"))
            .Returns(new ValidationResult(true));
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("ZZZ"))
            .ReturnsAsync(Result<TokenInfo>.Ok(new TokenInfo("ZZZ", "Zero", 100m, "", "")));
        _m.OrderValidation
            .Setup(s => s.ValidateTokenAsync(UserId, "zzz", "купить"))
            .ReturnsAsync(new ValidationResult(true));
        _m.OrderValidation
            .Setup(s => s.ValidateQuantity(5))
            .Returns(new ValidationResult(true));

        await _engine.ProcessInput(UserId, "/place_order");
        await _engine.ProcessInput(UserId, "Купить");
        await _engine.ProcessInput(UserId, "zzz");
        await _engine.ProcessInput(UserId, "5");
        var result = await _engine.ProcessInput(UserId, "abc");

        Assert.NotNull(result.Message);
        Assert.Contains("Ошибка", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  /cancel_order
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task CancelOrder_NoActiveOrders_ReturnsNoOrdersMessage()
    {
        _m.OrderCancellation
            .Setup(s => s.HasActiveOrdersAsync(UserId))
            .ReturnsAsync(false);

        var result = await _engine.ProcessInput(UserId, "/cancel_order");

        Assert.NotNull(result.Message);
        Assert.Equal("Нет активных ордеров для отмены.", result.Message);
    }

    [Fact]
    public async Task CancelOrder_WithActiveOrder_ShowsSelectQuestion()
    {
        _m.OrderCancellation
            .Setup(s => s.HasActiveOrdersAsync(UserId))
            .ReturnsAsync(true);

        var result = await _engine.ProcessInput(UserId, "/cancel_order");

        Assert.NotNull(result.Message);
        Assert.Equal("Какой ордер хотите отменить?", result.Message);
    }

    [Fact]
    public async Task CancelOrder_SelectOrder_ShowsConfirmation()
    {
        _m.OrderCancellation
            .Setup(s => s.HasActiveOrdersAsync(UserId))
            .ReturnsAsync(true);
        _m.OrderValidation
            .Setup(s => s.ValidateOrderCancellationAsync(UserId, "order-1"))
            .ReturnsAsync(new ValidationResult(true));

        await _engine.ProcessInput(UserId, "/cancel_order");
        var result = await _engine.ProcessInput(UserId, "order-1");

        Assert.NotNull(result.Message);
        Assert.Equal("Вы уверены что хотите отменить ордер?", result.Message);
        Assert.NotNull(result.Buttons);
        Assert.Equal(2, result.Buttons.Count);
        Assert.Equal("✅ Да, отменить", result.Buttons[0].Text);
        Assert.Equal("❌ Нет, оставить", result.Buttons[1].Text);
    }

    [Fact]
    public async Task CancelOrder_ConfirmCancelsSuccessfully()
    {
        _m.OrderCancellation
            .Setup(s => s.HasActiveOrdersAsync(UserId))
            .ReturnsAsync(true);
        _m.OrderValidation
            .Setup(s => s.ValidateOrderCancellationAsync(UserId, "order-1"))
            .ReturnsAsync(new ValidationResult(true));
        _m.OrderCancellation
            .Setup(s => s.CancelOrderAsync(UserId, "order-1"))
            .ReturnsAsync(Result.Ok());

        await _engine.ProcessInput(UserId, "/cancel_order");
        await _engine.ProcessInput(UserId, "order-1");
        var result = await _engine.ProcessInput(UserId, "confirm");

        Assert.NotNull(result.Message);
        Assert.Equal("Ордер успешно отменён", result.Message);
    }

    [Fact]
    public async Task CancelOrder_DeclineDoesNotCancel()
    {
        _m.OrderCancellation
            .Setup(s => s.HasActiveOrdersAsync(UserId))
            .ReturnsAsync(true);
        _m.OrderValidation
            .Setup(s => s.ValidateOrderCancellationAsync(UserId, "order-1"))
            .ReturnsAsync(new ValidationResult(true));

        await _engine.ProcessInput(UserId, "/cancel_order");
        await _engine.ProcessInput(UserId, "order-1");
        var result = await _engine.ProcessInput(UserId, "отмена");

        Assert.NotNull(result.Message);
        Assert.Equal("Отмена не подтверждена", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  /cancel_all_orders
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task CancelAllOrders_NoActiveOrders_ReturnsNoOrdersMessage()
    {
        _m.OrderCancellation
            .Setup(s => s.HasActiveOrdersAsync(UserId))
            .ReturnsAsync(false);

        var result = await _engine.ProcessInput(UserId, "/cancel_all_orders");

        Assert.NotNull(result.Message);
        Assert.Equal("Нет активных ордеров для отмены.", result.Message);
    }

    [Fact]
    public async Task CancelAllOrders_WithOrders_ShowsConfirmation()
    {
        _m.OrderCancellation
            .Setup(s => s.HasActiveOrdersAsync(UserId))
            .ReturnsAsync(true);

        var result = await _engine.ProcessInput(UserId, "/cancel_all_orders");

        Assert.NotNull(result.Message);
        Assert.Equal("Вы уверены что хотите отменить все активные ордера?", result.Message);
        Assert.NotNull(result.Buttons);
        Assert.Equal(2, result.Buttons.Count);
    }

    [Fact]
    public async Task CancelAllOrders_ConfirmCancelsAll()
    {
        _m.OrderCancellation
            .Setup(s => s.HasActiveOrdersAsync(UserId))
            .ReturnsAsync(true);
        _m.OrderCancellation
            .Setup(s => s.CancelAllOrderAsync(UserId))
            .ReturnsAsync(Result<int>.Ok(2));

        await _engine.ProcessInput(UserId, "/cancel_all_orders");
        var result = await _engine.ProcessInput(UserId, "confirm");

        Assert.NotNull(result.Message);
        Assert.Equal("Всего успешно отменено ордеров: 2", result.Message);
    }

    [Fact]
    public async Task CancelAllOrders_DeclineDoesNotCancel()
    {
        _m.OrderCancellation
            .Setup(s => s.HasActiveOrdersAsync(UserId))
            .ReturnsAsync(true);

        await _engine.ProcessInput(UserId, "/cancel_all_orders");
        var result = await _engine.ProcessInput(UserId, "отмена");

        Assert.NotNull(result.Message);
        Assert.Equal("Отмена не подтверждена", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  /get_token_info
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetTokenInfo_ShowsSelectTokenQuestion()
    {
        var result = await _engine.ProcessInput(UserId, "/get_token_info");

        Assert.NotNull(result.Message);
        Assert.Equal("Какой токен вы хотите посмотреть?", result.Message);
    }

    [Fact]
    public async Task GetTokenInfo_ValidToken_ReturnsTokenInfo()
    {
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("ZZZ"))
            .ReturnsAsync(Result<TokenInfo>.Ok(new TokenInfo("ZZZ", "Zero", 70.85m, "", "")));

        await _engine.ProcessInput(UserId, "/get_token_info");
        var result = await _engine.ProcessInput(UserId, "ZZZ");

        Assert.NotNull(result.Message);
        Assert.Contains("📊 Информация о токене", result.Message);
        Assert.Contains("Символ: ZZZ", result.Message);
        Assert.Contains("Название: Zero", result.Message);
        Assert.Contains("70,85₽", result.Message);
    }

    [Fact]
    public async Task GetTokenInfo_InvalidToken_ReturnsNotFoundError()
    {
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("NONEXISTENT"))
            .ReturnsAsync(Result<TokenInfo>.Fail("Token not found"));

        await _engine.ProcessInput(UserId, "/get_token_info");
        var result = await _engine.ProcessInput(UserId, "NONEXISTENT");

        Assert.NotNull(result.Message);
        Assert.Equal("Ошибка на стороне сервера", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  /get_price_history
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetPriceHistory_ShowsSelectTokenQuestion()
    {
        var result = await _engine.ProcessInput(UserId, "/get_price_history");

        Assert.NotNull(result.Message);
        Assert.Equal("Какой токен вы хотите посмотреть?", result.Message);
    }

    [Fact]
    public async Task GetPriceHistory_SelectToken_ShowsTimeframeQuestion()
    {
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("ZZZ"))
            .ReturnsAsync(Result<TokenInfo>.Ok(new TokenInfo("ZZZ", "Zero", 100m, "", "")));

        await _engine.ProcessInput(UserId, "/get_price_history");
        var result = await _engine.ProcessInput(UserId, "ZZZ");

        Assert.NotNull(result.Message);
        Assert.Equal("Какой шаг свечи (в минутах)?", result.Message);
        Assert.NotNull(result.Buttons);
        Assert.Equal(5, result.Buttons.Count);
    }

    [Fact]
    public async Task GetPriceHistory_FullFlow_ReturnsHistory()
    {
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("ZZZ"))
            .ReturnsAsync(Result<TokenInfo>.Ok(new TokenInfo("ZZZ", "Zero", 100m, "", "")));
        _m.CandleOrchestrator
            .Setup(s => s.GetAggregatedCandlesAsync("ZZZ", It.IsAny<DateTime>(), It.IsAny<DateTime>(), 5))
            .ReturnsAsync(Result<List<PriceCandleInfo>>.Ok(new List<PriceCandleInfo>
            {
                new(99m, 101m, 98m, 100m, new DateTime(2026, 7, 23, 8, 0, 0, DateTimeKind.Utc), 0),
                new(100m, 102m, 99m, 101m, new DateTime(2026, 7, 23, 8, 5, 0, DateTimeKind.Utc), 0)
            }));

        await _engine.ProcessInput(UserId, "/get_price_history");
        await _engine.ProcessInput(UserId, "ZZZ");
        await _engine.ProcessInput(UserId, "5");
        var result = await _engine.ProcessInput(UserId, "10");

        Assert.NotNull(result.Message);
        Assert.Contains("История цен ZZZ", result.Message);
        Assert.Contains("шаг 5 мин", result.Message);
        Assert.Contains("2 записей", result.Message);
        Assert.Contains("100,00₽", result.Message);
        Assert.Contains("101,00₽", result.Message);
    }

    [Fact]
    public async Task GetPriceHistory_NoData_ShowsNoDataMessage()
    {
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("ZZZ"))
            .ReturnsAsync(Result<TokenInfo>.Ok(new TokenInfo("ZZZ", "Zero", 100m, "", "")));
        _m.CandleOrchestrator
            .Setup(s => s.GetAggregatedCandlesAsync("ZZZ", It.IsAny<DateTime>(), It.IsAny<DateTime>(), 5))
            .ReturnsAsync(Result<List<PriceCandleInfo>>.Ok(new List<PriceCandleInfo>()));

        await _engine.ProcessInput(UserId, "/get_price_history");
        await _engine.ProcessInput(UserId, "ZZZ");
        await _engine.ProcessInput(UserId, "5");
        var result = await _engine.ProcessInput(UserId, "10");

        Assert.NotNull(result.Message);
        Assert.Contains("Нет данных по свечам", result.Message);
        Assert.Contains("ZZZ", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  /get_order_book
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetOrderBook_ShowsSelectTokenQuestion()
    {
        var result = await _engine.ProcessInput(UserId, "/get_order_book");

        Assert.NotNull(result.Message);
        Assert.Equal("Какой токен вы хотите посмотреть в стакане?", result.Message);
    }

    [Fact]
    public async Task GetOrderBook_SelectToken_ShowsBuyCountQuestion()
    {
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("ZZZ"))
            .ReturnsAsync(Result<TokenInfo>.Ok(new TokenInfo("ZZZ", "Zero", 100m, "", "")));

        await _engine.ProcessInput(UserId, "/get_order_book");
        var result = await _engine.ProcessInput(UserId, "ZZZ");

        Assert.NotNull(result.Message);
        Assert.Equal("Сколько ордеров на покупку показать?", result.Message);
        Assert.NotNull(result.Buttons);
        Assert.Equal(4, result.Buttons.Count);
    }

    [Fact]
    public async Task GetOrderBook_SetBuyCount_ShowsSellCountQuestion()
    {
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("ZZZ"))
            .ReturnsAsync(Result<TokenInfo>.Ok(new TokenInfo("ZZZ", "Zero", 100m, "", "")));

        await _engine.ProcessInput(UserId, "/get_order_book");
        await _engine.ProcessInput(UserId, "ZZZ");
        var result = await _engine.ProcessInput(UserId, "5");

        Assert.NotNull(result.Message);
        Assert.Equal("Сколько ордеров на продажу показать?", result.Message);
    }

    [Fact]
    public async Task GetOrderBook_FullFlow_ReturnsFormattedOrderBook()
    {
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("ZZZ"))
            .ReturnsAsync(Result<TokenInfo>.Ok(new TokenInfo("ZZZ", "Zero", 70m, "", "")));

        var book = new OrderBookResult("ZZZ", 70.10m, 70.34m, 0.24m,
            new List<OrderBookEntry>
            {
                new("Buy", 70.10m, 3, 210.30m),
                new("Buy", 69.99m, 3, 209.97m),
                new("Buy", 69.94m, 3, 209.82m)
            },
            new List<OrderBookEntry>
            {
                new("Sell", 70.34m, 6, 422.04m),
                new("Sell", 70.45m, 3, 211.35m),
                new("Sell", 70.66m, 15, 1059.90m)
            });
        _m.OrderBook
            .Setup(s => s.GetOrderBookAsync("ZZZ", 5, 5))
            .ReturnsAsync(Result<OrderBookResult>.Ok(book));

        await _engine.ProcessInput(UserId, "/get_order_book");
        await _engine.ProcessInput(UserId, "ZZZ");
        await _engine.ProcessInput(UserId, "5");
        var result = await _engine.ProcessInput(UserId, "5");

        Assert.NotNull(result.Message);
        var msg = Normalize(result.Message);
        Assert.Contains("Стакан ордеров ZZZ", msg);
        Assert.Contains("🔺 ПРОДАЖА (ASK)", msg);
        Assert.Contains("🔻 ПОКУПКА (BID)", msg);
        Assert.Contains("ℹ️ КАК ЧИТАТЬ:", msg);
        Assert.Contains("номер] [цена] × [количество]", msg);
        Assert.Contains("70,34", msg);
        Assert.Contains("70,10", msg);

        Assert.NotNull(result.Buttons);
        Assert.Single(result.Buttons);
        Assert.Equal("🔄 Обновить", result.Buttons[0].Text);
        Assert.Equal("/get_order_book ZZZ 5 5", result.Buttons[0].Value);
    }

    [Fact]
    public async Task GetOrderBook_EmptyBook_ShowsEmptyMessage()
    {
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("ZZZ"))
            .ReturnsAsync(Result<TokenInfo>.Ok(new TokenInfo("ZZZ", "Zero", 100m, "", "")));

        var book = new OrderBookResult("ZZZ", 0, 0, 0,
            new List<OrderBookEntry>(), new List<OrderBookEntry>());
        _m.OrderBook
            .Setup(s => s.GetOrderBookAsync("ZZZ", 5, 5))
            .ReturnsAsync(Result<OrderBookResult>.Ok(book));

        await _engine.ProcessInput(UserId, "/get_order_book");
        await _engine.ProcessInput(UserId, "ZZZ");
        await _engine.ProcessInput(UserId, "5");
        var result = await _engine.ProcessInput(UserId, "5");

        Assert.NotNull(result.Message);
        Assert.Contains("Стакан пуст", result.Message);
    }

    [Fact]
    public async Task GetOrderBook_QuickPath_ReturnsOrderBook()
    {
        var book = new OrderBookResult("ZZZ", 70.10m, 70.34m, 0.24m,
            new List<OrderBookEntry>
            {
                new("Buy", 70.10m, 3, 210.30m)
            },
            new List<OrderBookEntry>
            {
                new("Sell", 70.34m, 6, 422.04m)
            });
        _m.OrderBook
            .Setup(s => s.GetOrderBookAsync("ZZZ", 5, 5))
            .ReturnsAsync(Result<OrderBookResult>.Ok(book));

        var result = await _engine.ProcessInput(UserId, "/get_order_book ZZZ 5 5");

        Assert.NotNull(result.Message);
        Assert.Contains("Стакан ордеров ZZZ", result.Message);
        Assert.Contains("ПРОДАЖА", result.Message);
        Assert.Contains("ПОКУПКА", result.Message);
        Assert.NotNull(result.Buttons);
        Assert.Equal("/get_order_book ZZZ 5 5", result.Buttons[0].Value);
    }

    [Fact]
    public async Task GetOrderBook_QuickPath_InvalidCount_ReturnsError()
    {
        var result = await _engine.ProcessInput(UserId, "/get_order_book ZZZ abc 5");

        Assert.NotNull(result.Message);
        Assert.Contains("положительное целое число", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  /get_orders
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetOrders_NoOrders_ReturnsEmptyMessage()
    {
        _m.OrderQuery
            .Setup(s => s.GetTraderOrdersAsync(UserId, true, false, false, false))
            .ReturnsAsync(Result<List<OrderInfo>>.Ok(new List<OrderInfo>()));

        var result = await _engine.ProcessInput(UserId, "/get_orders");

        Assert.NotNull(result.Message);
        Assert.Equal("У вас нет активных ордеров.", result.Message);
    }

    [Fact]
    public async Task GetOrders_WithActiveOrders_ShowsOrdersWithProgressBar()
    {
        _m.OrderQuery
            .Setup(s => s.GetTraderOrdersAsync(UserId, true, false, false, false))
            .ReturnsAsync(Result<List<OrderInfo>>.Ok(new List<OrderInfo>
            {
                new("order-1", "ZZZ", "Zero", "Buy", 65, 0, 0, 58.29m, "Active"),
                new("order-2", "ZZZ", "Zero", "Sell", 10, 5, 50, 75m, "Active")
            }));

        var result = await _engine.ProcessInput(UserId, "/get_orders");

        Assert.NotNull(result.Message);
        var msg = Normalize(result.Message);
        Assert.Contains("📋 Ваши активные ордера:", msg);
        Assert.Contains("🟢 Покупка ZZZ", msg);
        Assert.Contains("🔴 Продажа ZZZ", msg);
        Assert.Contains("58,29₽", msg);
        Assert.Contains("75,00₽", msg);
        Assert.Contains("░░░░░░░░░░", msg);
        Assert.Contains("0/65 (0%)", msg);
        Assert.Contains("5/10 (50%)", msg);

        Assert.NotNull(result.Buttons);
        Assert.Single(result.Buttons);
        Assert.Equal("🔄 Обновить", result.Buttons[0].Text);
        Assert.Equal("/get_orders", result.Buttons[0].Value);
    }

    // ═══════════════════════════════════════════════════════════
    //  /get_trades
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetTrades_ShowsLimitQuestion()
    {
        var result = await _engine.ProcessInput(UserId, "/get_trades");

        Assert.NotNull(result.Message);
        Assert.Equal("Сколько последних сделок показать?", result.Message);
        Assert.NotNull(result.Buttons);
        Assert.Equal(4, result.Buttons.Count);
        Assert.Contains(result.Buttons, b => b.Value == "5");
        Assert.Contains(result.Buttons, b => b.Value == "50");
    }

    [Fact]
    public async Task GetTrades_WithTrades_ShowsTradesList()
    {
        _m.TradeQuery
            .Setup(s => s.GetTraderTradesAsync(UserId, true))
            .ReturnsAsync(Result<List<TradeInfo>>.Ok(new List<TradeInfo>
            {
                new("Buyer", 103.50m, 8, -828m, new DateTime(2026, 7, 18, 15, 36, 0), new TokenInfo("LAPLD", "Lappland", 97m, "", "")),
                new("Seller", 98.02m, 2, 196.05m, new DateTime(2026, 7, 23, 11, 53, 0), new TokenInfo("LAPLD", "Lappland", 97m, "", ""))
            }));

        await _engine.ProcessInput(UserId, "/get_trades");
        var result = await _engine.ProcessInput(UserId, "5");

        Assert.NotNull(result.Message);
        var msg = Normalize(result.Message);
        Assert.Contains("📊 Последние 2 сделок:", msg);
        Assert.Contains("🟢 Купил LAPLD", msg);
        Assert.Contains("🔴 Продал LAPLD", msg);
        Assert.Contains("Цена: 103,50 | Кол-во: 8", msg);
        Assert.Contains("Цена: 98,02 | Кол-во: 2", msg);
        Assert.Contains("💸 Баланс: -828,00₽", msg);
        Assert.Contains("💰 Баланс: +196,05₽", msg);

        Assert.NotNull(result.Buttons);
        Assert.Equal("/get_trades 5", result.Buttons[0].Value);
    }

    [Fact]
    public async Task GetTrades_NoTrades_ShowsEmptyMessage()
    {
        _m.TradeQuery
            .Setup(s => s.GetTraderTradesAsync(UserId, true))
            .ReturnsAsync(Result<List<TradeInfo>>.Ok(new List<TradeInfo>()));

        await _engine.ProcessInput(UserId, "/get_trades");
        var result = await _engine.ProcessInput(UserId, "5");

        Assert.NotNull(result.Message);
        Assert.Equal("У вас пока нет сделок.", result.Message);
    }

    [Fact]
    public async Task GetTrades_InvalidLimit_ShowsServerError()
    {
        await _engine.ProcessInput(UserId, "/get_trades");
        var result = await _engine.ProcessInput(UserId, "abc");

        Assert.NotNull(result.Message);
        Assert.Contains("Ошибка", result.Message);
    }

    [Fact]
    public async Task GetTrades_QuickPath_ReturnsTrades()
    {
        _m.TradeQuery
            .Setup(s => s.GetTraderTradesAsync(UserId, true))
            .ReturnsAsync(Result<List<TradeInfo>>.Ok(new List<TradeInfo>
            {
                new("Buyer", 103.50m, 8, -828m, new DateTime(2026, 7, 18, 15, 36, 0), new TokenInfo("LAPLD", "Lappland", 97m, "", ""))
            }));

        var result = await _engine.ProcessInput(UserId, "/get_trades 10");

        Assert.NotNull(result.Message);
        Assert.Contains("📊 Последние 1 сделок:", result.Message);
        Assert.Contains("Купил LAPLD", result.Message);
        Assert.NotNull(result.Buttons);
        Assert.Equal("/get_trades 10", result.Buttons[0].Value);
    }

    [Fact]
    public async Task GetTrades_QuickPath_InvalidLimit_ReturnsError()
    {
        var result = await _engine.ProcessInput(UserId, "/get_trades abc");

        Assert.NotNull(result.Message);
        Assert.Contains("положительное целое число", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  /get_tops
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetTops_ShowsLimitQuestion()
    {
        var result = await _engine.ProcessInput(UserId, "/get_tops");

        Assert.NotNull(result.Message);
        Assert.Equal("Сколько трейдеров показать в рейтинге?", result.Message);
        Assert.NotNull(result.Buttons);
        Assert.Equal(4, result.Buttons.Count);
    }

    [Fact]
    public async Task GetTops_EmptyRanking_ReturnsEmptyMessage()
    {
        _m.LeadersTop
            .Setup(s => s.GetTopAsync(5))
            .ReturnsAsync(Result<List<LeaderEntry>>.Ok(new List<LeaderEntry>()));

        await _engine.ProcessInput(UserId, "/get_tops");
        var result = await _engine.ProcessInput(UserId, "5");

        Assert.NotNull(result.Message);
        Assert.Equal("Рейтинг пока пуст.", result.Message);
    }

    [Fact]
    public async Task GetTops_WithRanking_ShowsLeaderboard()
    {
        _m.LeadersTop
            .Setup(s => s.GetTopAsync(5))
            .ReturnsAsync(Result<List<LeaderEntry>>.Ok(new List<LeaderEntry>
            {
                new(1, 100, "HasHas", 13097.80m),
                new(2, UserId, "Alice", 6452.79m),
                new(3, 102, "Bob", 2500m)
            }));
        _m.LeadersTop
            .Setup(s => s.GetLocalTopAsync(UserId, 2, 2))
            .ReturnsAsync(Result<List<LeaderEntry>>.Ok(new List<LeaderEntry>
            {
                new(1, 100, "HasHas", 13097.80m),
                new(2, UserId, "Alice", 6452.79m),
                new(3, 102, "Bob", 2500m)
            }));

        await _engine.ProcessInput(UserId, "/get_tops");
        var result = await _engine.ProcessInput(UserId, "5");

        Assert.NotNull(result.Message);
        var msg = Normalize(result.Message);
        Assert.Contains("🏆 Топ-3 трейдеров:", msg);
        Assert.Contains("🥇 HasHas", msg);
        Assert.Contains("🥈 Alice", msg);
        Assert.Contains("🥉 Bob", msg);
        Assert.Contains("← Вы", msg);
        Assert.Contains("📍 Ваше окружение:", msg);
        Assert.Contains("#1 HasHas", msg);
        Assert.Contains("#3 Bob", msg);

        Assert.NotNull(result.Buttons);
        Assert.Equal("/get_tops 5", result.Buttons[0].Value);
    }

    [Fact]
    public async Task GetTops_QuickPath_ReturnsLeaderboard()
    {
        _m.LeadersTop
            .Setup(s => s.GetTopAsync(5))
            .ReturnsAsync(Result<List<LeaderEntry>>.Ok(new List<LeaderEntry>
            {
                new(1, 100, "HasHas", 13097.80m),
                new(2, UserId, "Alice", 6452.79m)
            }));
        _m.LeadersTop
            .Setup(s => s.GetLocalTopAsync(UserId, 2, 2))
            .ReturnsAsync(Result<List<LeaderEntry>>.Ok(new List<LeaderEntry>
            {
                new(1, 100, "HasHas", 13097.80m),
                new(2, UserId, "Alice", 6452.79m)
            }));

        var result = await _engine.ProcessInput(UserId, "/get_tops 5");

        Assert.NotNull(result.Message);
        Assert.Contains("🏆 Топ-2 трейдеров:", result.Message);
        Assert.Contains("← Вы", result.Message);
        Assert.NotNull(result.Buttons);
        Assert.Equal("/get_tops 5", result.Buttons[0].Value);
    }

    [Fact]
    public async Task GetTops_QuickPath_InvalidLimit_ReturnsError()
    {
        var result = await _engine.ProcessInput(UserId, "/get_tops abc");

        Assert.NotNull(result.Message);
        Assert.Contains("положительное целое число", result.Message);
    }

    [Fact]
    public async Task GetTops_NoLocalTop_SkipsLocalSection()
    {
        _m.LeadersTop
            .Setup(s => s.GetTopAsync(5))
            .ReturnsAsync(Result<List<LeaderEntry>>.Ok(new List<LeaderEntry>
            {
                new(1, 100, "HasHas", 13097.80m),
                new(2, UserId, "Alice", 6452.79m),
                new(3, 102, "Bob", 2500m)
            }));
        _m.LeadersTop
            .Setup(s => s.GetLocalTopAsync(UserId, 2, 2))
            .ReturnsAsync(Result<List<LeaderEntry>>.Ok(new List<LeaderEntry>
            {
                new(1, 100, "HasHas", 13097.80m)
            }));

        await _engine.ProcessInput(UserId, "/get_tops");
        var result = await _engine.ProcessInput(UserId, "5");

        Assert.NotNull(result.Message);
        var msg = Normalize(result.Message);
        Assert.Contains("🏆 Топ-3 трейдеров:", msg);
        Assert.DoesNotContain("📍 Ваше окружение:", msg);
    }

    // ═══════════════════════════════════════════════════════════
    //  Unknown command
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task UnknownCommand_ReturnsUnknownMessage()
    {
        var result = await _engine.ProcessInput(UserId, "/unknown_command");

        Assert.NotNull(result.Message);
        Assert.Equal("Неизвестная команда", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  Admin commands
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminHelp_ShowsHelpText()
    {
        var result = await _engine.ProcessInput(UserId, "/admin_help");

        Assert.NotNull(result.Message);
        Assert.Contains("Admin commands:", result.Message);
        Assert.Contains("/admin_help_trader", result.Message);
        Assert.Contains("/admin_help_token", result.Message);
        Assert.Contains("/admin_help_other", result.Message);
    }

    [Fact]
    public async Task AdminHelpTrader_ShowsTraderHelp()
    {
        var result = await _engine.ProcessInput(UserId, "/admin_help_trader");

        Assert.NotNull(result.Message);
        Assert.Contains("Trader commands:", result.Message);
        Assert.Contains("/admin_set_token_to_user", result.Message);
        Assert.Contains("/admin_add_balance_to_user", result.Message);
    }

    [Fact]
    public async Task AdminHelpToken_ShowsTokenHelp()
    {
        var result = await _engine.ProcessInput(UserId, "/admin_help_token");

        Assert.NotNull(result.Message);
        Assert.Contains("Token commands:", result.Message);
        Assert.Contains("/admin_create_token", result.Message);
        Assert.Contains("/admin_bots_activity", result.Message);
    }

    [Fact]
    public async Task AdminHelpOther_ShowsOtherHelp()
    {
        var result = await _engine.ProcessInput(UserId, "/admin_help_other");

        Assert.NotNull(result.Message);
        Assert.Contains("Other commands:", result.Message);
        Assert.Contains("/admin_broadcast", result.Message);
        Assert.Contains("/admin_stats", result.Message);
    }

    [Fact]
    public async Task AdminCreateToken_ShowsJsonPrompt()
    {
        var result = await _engine.ProcessInput(UserId, "/admin_create_token");

        Assert.NotNull(result.Message);
        Assert.Contains("symbol", result.Message);
        Assert.Contains("name", result.Message);
        Assert.Contains("startPrice", result.Message);
    }

    [Fact]
    public async Task AdminCreateToken_ValidJson_CreatesToken()
    {
        _m.TokenCreation
            .Setup(s => s.CreateTokenAsync(It.IsAny<CreateTokenCommand>()))
            .ReturnsAsync(Result<TokenCreationData>.Ok(new TokenCreationData()));

        await _engine.ProcessInput(UserId, "/admin_create_token");
        var json = """{"symbol": "TEST", "name": "Test", "rarity": 3, "startPrice": 100, "totalSupply": 1000, "isActive": true, "imageUrl": "", "iconUrl": ""}""";
        var result = await _engine.ProcessInput(UserId, json);

        Assert.NotNull(result.Message);
        Assert.Equal("Token create successful", result.Message);
    }

    [Fact]
    public async Task AdminBotsActivity_ShowsSelectTokenQuestion()
    {
        var result = await _engine.ProcessInput(UserId, "/admin_bots_activity");

        Assert.NotNull(result.Message);
        Assert.Equal("Select token symbol to view bots:", result.Message);
    }

    [Fact]
    public async Task AdminBotsReconstruction_ShowsJsonPrompt()
    {
        var result = await _engine.ProcessInput(UserId, "/admin_bots_reconstruction");

        Assert.NotNull(result.Message);
        Assert.Contains("botId", result.Message);
    }

    [Fact]
    public async Task AdminGenerateAuthToken_ShowsJsonPrompt()
    {
        var result = await _engine.ProcessInput(UserId, "/admin_generate_auth_token");

        Assert.NotNull(result.Message);
        Assert.Contains("telegramId", result.Message);
    }

    [Fact]
    public async Task AdminStats_ShowsStatistics()
    {
        _m.TraderQuery
            .Setup(s => s.GetTraderCountAsync())
            .ReturnsAsync(Result<int>.Ok(5));

        _m.TradingVolume
            .Setup(s => s.GetTotalVolumeAsync(0, false))
            .ReturnsAsync(Result<decimal>.Ok(15000m));

        _m.TradingVolume
            .Setup(s => s.GetVolumePerTokenAsync(0, false))
            .ReturnsAsync(Result<List<(string, decimal)>>.Ok(
                new List<(string, decimal)> { ("ZZZ", 10000m), ("YYY", 5000m) }));

        var result = await _engine.ProcessInput(UserId, "/admin_stats");

        Assert.NotNull(result.Message);
        Assert.Contains("System Statistics", result.Message);
        Assert.Contains("All time", result.Message);
        Assert.Contains("Registered traders: 5", result.Message);
        Assert.Contains("Total volume (no bots): 15000", result.Message);
        Assert.Contains("Volume per token:", result.Message);
        Assert.Contains("ZZZ", result.Message);
        Assert.Contains("YYY", result.Message);
        Assert.NotNull(result.Buttons);
        Assert.Contains(result.Buttons, b => b.Value == "/admin_stats 0");
        Assert.Contains(result.Buttons, b => b.Value == "/admin_stats 1");
        Assert.Contains(result.Buttons, b => b.Value == "/admin_stats 7");
        Assert.Contains(result.Buttons, b => b.Value == "/admin_stats 30");
        Assert.Contains(result.Buttons, b => b.Value == "/admin_stats 180");
        Assert.Contains(result.Buttons, b => b.Value == "/admin_stats 365");
    }

    [Fact]
    public async Task AdminStats_NoTradeData_ShowsNoDataMessage()
    {
        _m.TraderQuery
            .Setup(s => s.GetTraderCountAsync())
            .ReturnsAsync(Result<int>.Ok(3));

        _m.TradingVolume
            .Setup(s => s.GetTotalVolumeAsync(0, false))
            .ReturnsAsync(Result<decimal>.Ok(0m));

        _m.TradingVolume
            .Setup(s => s.GetVolumePerTokenAsync(0, false))
            .ReturnsAsync(Result<List<(string, decimal)>>.Ok(new List<(string, decimal)>()));

        var result = await _engine.ProcessInput(UserId, "/admin_stats");

        Assert.NotNull(result.Message);
        Assert.Contains("No trade data available", result.Message);
    }

    [Fact]
    public async Task AdminStats_QuickPath_ReturnsStatsForPeriod()
    {
        _m.TraderQuery
            .Setup(s => s.GetTraderCountAsync())
            .ReturnsAsync(Result<int>.Ok(5));

        _m.TradingVolume
            .Setup(s => s.GetTotalVolumeAsync(7, false))
            .ReturnsAsync(Result<decimal>.Ok(8000m));

        _m.TradingVolume
            .Setup(s => s.GetVolumePerTokenAsync(7, false))
            .ReturnsAsync(Result<List<(string, decimal)>>.Ok(new List<(string, decimal)> { ("ZZZ", 8000m) }));

        var result = await _engine.ProcessInput(UserId, "/admin_stats 7");

        Assert.NotNull(result.Message);
        Assert.Contains("System Statistics", result.Message);
        Assert.Contains("Last 7 days", result.Message);
        Assert.Contains("Total volume (no bots): 8000", result.Message);
        Assert.NotNull(result.Buttons);
    }

    [Fact]
    public async Task AdminGetIds_ShowsTraderList()
    {
        var traders = new List<(string Username, long TelegramId)>
        {
            ("Alice", 101),
            ("Bob", 200),
            ("Charlie", 1500)
        };

        _m.TraderQuery
            .Setup(s => s.GetAllTradersWithoutBotsAsync())
            .ReturnsAsync(Result<List<(string Username, long TelegramId)>>.Ok(traders));

        var result = await _engine.ProcessInput(UserId, "/admin_get_ids");

        Assert.NotNull(result.Message);
        Assert.Contains("Alice", result.Message);
        Assert.Contains("101", result.Message);
        Assert.Contains("Bob", result.Message);
        Assert.Contains("200", result.Message);
        Assert.Contains("Charlie", result.Message);
        Assert.Contains("1500", result.Message);
    }

    [Fact]
    public async Task AdminGetIds_Error_ShowsErrorMessage()
    {
        _m.TraderQuery
            .Setup(s => s.GetAllTradersWithoutBotsAsync())
            .ReturnsAsync(Result<List<(string Username, long TelegramId)>>.Fail("DB error"));

        var result = await _engine.ProcessInput(UserId, "/admin_get_ids");

        Assert.NotNull(result.Message);
        Assert.Equal("Failed to get trader list.", result.Message);
    }

    [Fact]
    public async Task AdminCreateTokens_ShowsJsonArrayPrompt()
    {
        var result = await _engine.ProcessInput(UserId, "/admin_create_tokens");

        Assert.NotNull(result.Message);
        Assert.Contains("JSON array", result.Message);
        Assert.Contains("symbol", result.Message);
    }

    [Fact]
    public async Task AdminCreateTokens_ValidJsonArray_CreatesTokens()
    {
        _m.TokenCreation
            .Setup(s => s.CreateTokenAsync(It.IsAny<CreateTokenCommand>()))
            .ReturnsAsync(Result<TokenCreationData>.Ok(new TokenCreationData()));

        await _engine.ProcessInput(UserId, "/admin_create_tokens");
        var json = """[{"symbol": "SHZA", "name": "Loony", "rarity": 3, "startPrice": 100, "totalSupply": 1000, "isActive": true, "imageUrl": "img", "iconUrl": "icon"}, {"symbol": "BLHD", "name": "Bloodhound", "rarity": 3, "startPrice": 100, "totalSupply": 1000, "isActive": true, "imageUrl": "img", "iconUrl": "icon"}]""";
        var result = await _engine.ProcessInput(UserId, json);

        Assert.NotNull(result.Message);
        Assert.Contains("SHZA (Loony) created", result.Message);
        Assert.Contains("BLHD (Bloodhound) created", result.Message);
    }

    [Fact]
    public async Task AdminCreateTokens_PartialFailure_ReportsPerToken()
    {
        _m.TokenCreation
            .Setup(s => s.CreateTokenAsync(It.Is<CreateTokenCommand>(c => c.Symbol == "SHZA")))
            .ReturnsAsync(Result<TokenCreationData>.Ok(new TokenCreationData()));
        _m.TokenCreation
            .Setup(s => s.CreateTokenAsync(It.Is<CreateTokenCommand>(c => c.Symbol == "BLHD")))
            .ReturnsAsync(Result<TokenCreationData>.Fail("Такой токен уже существует"));

        await _engine.ProcessInput(UserId, "/admin_create_tokens");
        var json = """[{"symbol": "SHZA", "name": "Loony", "rarity": 3, "startPrice": 100, "totalSupply": 1000, "isActive": true, "imageUrl": "img", "iconUrl": "icon"}, {"symbol": "BLHD", "name": "Bloodhound", "rarity": 3, "startPrice": 100, "totalSupply": 1000, "isActive": true, "imageUrl": "img", "iconUrl": "icon"}]""";
        var result = await _engine.ProcessInput(UserId, json);

        Assert.NotNull(result.Message);
        Assert.Contains("SHZA (Loony) created", result.Message);
        Assert.Contains("BLHD: Такой токен уже существует", result.Message);
    }

    [Fact]
    public async Task AdminCreateTokens_EmptyArray_ReturnsError()
    {
        await _engine.ProcessInput(UserId, "/admin_create_tokens");
        var result = await _engine.ProcessInput(UserId, "[]");

        Assert.NotNull(result.Message);
        Assert.Equal("Expected a JSON array with at least one token.", result.Message);
    }

    [Fact]
    public async Task AdminDeleteToken_ShowsSymbolQuestion()
    {
        var result = await _engine.ProcessInput(UserId, "/admin_delete_token");

        Assert.NotNull(result.Message);
        Assert.Equal("Enter token symbol to delete:", result.Message);
    }

    [Fact]
    public async Task AdminDeleteToken_UnknownSymbol_ReturnsError()
    {
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("NOPE"))
            .ReturnsAsync(Result<TokenInfo>.Fail("Токен не найден"));

        await _engine.ProcessInput(UserId, "/admin_delete_token");
        var result = await _engine.ProcessInput(UserId, "NOPE");

        Assert.NotNull(result.Message);
        Assert.Equal("Token not found. Check the symbol and try again.", result.Message);
    }

    [Fact]
    public async Task AdminDeleteToken_Confirm_DeletesToken()
    {
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("ZZZ"))
            .ReturnsAsync(Result<TokenInfo>.Ok(new TokenInfo("ZZZ", "Test", 100m, "icon", "image")));
        _m.TokenDeletion
            .Setup(s => s.DeleteTokenAsync("ZZZ"))
            .ReturnsAsync(Result.Ok());

        await _engine.ProcessInput(UserId, "/admin_delete_token");
        var confirmStep = await _engine.ProcessInput(UserId, "ZZZ");
        var result = await _engine.ProcessInput(UserId, "confirm");

        Assert.NotNull(confirmStep.Message);
        Assert.Contains("PERMANENTLY delete", confirmStep.Message);
        Assert.NotNull(confirmStep.Buttons);
        Assert.Contains(confirmStep.Buttons, b => b.Value == "confirm");
        Assert.Equal("Token ZZZ deleted.", result.Message);
    }

    [Fact]
    public async Task AdminDeleteToken_Cancel_DoesNotDelete()
    {
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("ZZZ"))
            .ReturnsAsync(Result<TokenInfo>.Ok(new TokenInfo("ZZZ", "Test", 100m, "icon", "image")));

        await _engine.ProcessInput(UserId, "/admin_delete_token");
        await _engine.ProcessInput(UserId, "ZZZ");
        var result = await _engine.ProcessInput(UserId, "cancel");

        _m.TokenDeletion.Verify(s => s.DeleteTokenAsync(It.IsAny<string>()), Times.Never);
        Assert.Equal("Deletion cancelled.", result.Message);
    }

    [Fact]
    public async Task AdminDeactivateToken_ShowsSymbolQuestion()
    {
        var result = await _engine.ProcessInput(UserId, "/admin_deactivate_token");

        Assert.NotNull(result.Message);
        Assert.Equal("Enter token symbol to deactivate:", result.Message);
    }

    [Fact]
    public async Task AdminDeactivateToken_Confirm_DeactivatesToken()
    {
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("ZZZ"))
            .ReturnsAsync(Result<TokenInfo>.Ok(new TokenInfo("ZZZ", "Test", 100m, "icon", "image")));
        _m.TokenDeletion
            .Setup(s => s.DeactivateTokenAsync("ZZZ"))
            .ReturnsAsync(Result.Ok());

        await _engine.ProcessInput(UserId, "/admin_deactivate_token");
        var confirmStep = await _engine.ProcessInput(UserId, "ZZZ");
        var result = await _engine.ProcessInput(UserId, "confirm");

        Assert.NotNull(confirmStep.Message);
        Assert.Contains("deactivate", confirmStep.Message);
        Assert.NotNull(confirmStep.Buttons);
        Assert.Contains(confirmStep.Buttons, b => b.Value == "confirm");
        Assert.Equal("Token ZZZ deactivated.", result.Message);
    }

    [Fact]
    public async Task AdminDeactivateToken_Cancel_DoesNotDeactivate()
    {
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("ZZZ"))
            .ReturnsAsync(Result<TokenInfo>.Ok(new TokenInfo("ZZZ", "Test", 100m, "icon", "image")));

        await _engine.ProcessInput(UserId, "/admin_deactivate_token");
        await _engine.ProcessInput(UserId, "ZZZ");
        var result = await _engine.ProcessInput(UserId, "cancel");

        _m.TokenDeletion.Verify(s => s.DeactivateTokenAsync(It.IsAny<string>()), Times.Never);
        Assert.Equal("Deactivation cancelled.", result.Message);
    }
}
