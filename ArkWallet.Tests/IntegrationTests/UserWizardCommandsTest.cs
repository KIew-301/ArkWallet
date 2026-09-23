using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.TradingContext.Application.Contracts.CharacterTokenServices;
using ArkWallet.Core.General.Application.Contracts.Leaders;
using ArkWallet.Core.TradingContext.Application.Contracts.MarketMaker;
using ArkWallet.Core.General.Application.Contracts.Other;
using ArkWallet.Core.TradingContext.Application.Contracts.Other;
using ArkWallet.Core.PortfolioContext.Application.Contracts.PortfolioServices;
using ArkWallet.Core.TradingContext.Application.Contracts.TradeOrderServices;
using ArkWallet.Core.TradingContext.Application.Services.TradeOrderServices;
using ArkWallet.Core.TradingContext.Application.Contracts.TradeServices;
using ArkWallet.Core.TradingContext.Application.Contracts.TraderServices;
using ArkWallet.Core.General.Application.Dtos;
using ArkWallet.Core.MiningContext.Application.Dtos;
using ArkWallet.Core.TradingContext.Application.Dtos;
using ArkWallet.Core.TradingContext.Application.Services.TraderServices;
using ArkWallet.Core.General.Application.Services.Wizard;
using ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Core.MailContext.Application.Contracts.MailServices;
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
        Assert.Contains($"💰 Баланс: {5000m:F2}{Descriptor.CurrencySymbol}", msg);
        Assert.Contains($"📊 Общий баланс: {6281.84m:F2}{Descriptor.CurrencySymbol}", msg);
        Assert.Contains("📦 Портфель:", msg);
        Assert.Contains($"LAPLD: 20 шт. (куплено за {2070m:F2}{Descriptor.CurrencySymbol})", msg);
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
        Assert.Contains($"💰 Баланс: {3000m:F2}{Descriptor.CurrencySymbol}", msg);
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
        Assert.Contains($"💰 Баланс: {4000m:F2}{Descriptor.CurrencySymbol}", msg);
        Assert.Contains($"📊 Общий баланс: {4000m:F2}{Descriptor.CurrencySymbol}", msg);
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
            .Setup(s => s.ValidateQuantity(5))
            .Returns(new ValidationResult(true));
        _m.OrderValidation
            .Setup(s => s.ValidatePrice(100m))
            .Returns(new ValidationResult(true));

        var orderDto = new OrderDto("order-1", OrderType.Buy, UserId, "ZZZ", 5, 100m,
            OrderStatus.Active, DateTime.UtcNow);
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
        Assert.Contains($"{100m:F2}{Descriptor.CurrencySymbol}", r5.Message);
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
            .Setup(s => s.ValidateQuantity(10))
            .Returns(new ValidationResult(true));
        _m.OrderValidation
            .Setup(s => s.ValidatePrice(150m))
            .Returns(new ValidationResult(true));

        var orderDto = new OrderDto("order-2", OrderType.Sell, UserId, "ZZZ", 10, 150m,
            OrderStatus.Active, DateTime.UtcNow);
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
        Assert.Contains($"{150m:F2}{Descriptor.CurrencySymbol}", result.Message);
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
        Assert.Contains("Неверное направление", result.Message);
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

        await _engine.ProcessInput(UserId, "/place_order");
        await _engine.ProcessInput(UserId, "Купить");
        await _engine.ProcessInput(UserId, "zzz");
        var result = await _engine.ProcessInput(UserId, "abc");

        Assert.NotNull(result.Message);
        Assert.Contains("Необходимо ввести целое число", result.Message);
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
            .Setup(s => s.ValidateQuantity(5))
            .Returns(new ValidationResult(true));
        _m.OrderValidation
            .Setup(s => s.ValidatePrice(100m))
            .Returns(new ValidationResult(true));

        await _engine.ProcessInput(UserId, "/place_order");
        await _engine.ProcessInput(UserId, "Купить");
        await _engine.ProcessInput(UserId, "zzz");
        await _engine.ProcessInput(UserId, "5");
        var result = await _engine.ProcessInput(UserId, "abc");

        Assert.NotNull(result.Message);
        Assert.Contains("Необходимо ввести число", result.Message);
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
        Assert.Contains($"{70.85m:F2}{Descriptor.CurrencySymbol}", result.Message);
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
        Assert.Contains("Токен не найден", result.Message);
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
        Assert.Contains($"{100m:F2}{Descriptor.CurrencySymbol}", result.Message);
        Assert.Contains($"{101m:F2}{Descriptor.CurrencySymbol}", result.Message);
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
        Assert.Contains($"{70.34m:F2}", msg);
        Assert.Contains($"{70.10m:F2}", msg);

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
        Assert.Contains($"{58.29m:F2}{Descriptor.CurrencySymbol}", msg);
        Assert.Contains($"{75m:F2}{Descriptor.CurrencySymbol}", msg);
        Assert.Contains("░░░░░░░░░░", msg);
        Assert.Contains("0/65 (0%)", msg);
        Assert.Contains("5/10 (50%)", msg);

        Assert.NotNull(result.Buttons);
        Assert.Single(result.Buttons);
        Assert.Equal("🔄 Обновить", result.Buttons[0].Text);
        Assert.Equal("/get_orders", result.Buttons[0].Value);
    }

    // ═══════════════════════════════════════════════════════════
    //  /get_tokens
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetTokens_ShowsAllTokensWithPricesAndChange()
    {
        _m.TokenQuery
            .Setup(s => s.GetAllActiveTokensAsync())
            .ReturnsAsync(Result<List<TokenInfoWithPriceChange>>.Ok(new List<TokenInfoWithPriceChange>
            {
                new(new TokenInfo("ZZZ", "Zero", 100m, "", ""), 5.5m),
                new(new TokenInfo("AAA", "Alpha", 50m, "", ""), -2.0m)
            }));

        var result = await _engine.ProcessInput(UserId, "/get_tokens");

        Assert.NotNull(result.Message);
        var msg = Normalize(result.Message);
        Assert.Contains("📊 Токены:", msg);
        Assert.Contains("ZZZ", msg);
        Assert.Contains("AAA", msg);
        Assert.True(msg.IndexOf("ZZZ") < msg.IndexOf("AAA"), "Tokens should be sorted by price (descending)");
        Assert.Contains($"{100m:F2}{Descriptor.CurrencySymbol}", msg);
        Assert.Contains($"+{5.5m:F1}%", msg);
        Assert.Contains($"-{2m:F1}%", msg);
        Assert.Contains("Всего: 2 токенов", msg);

        Assert.NotNull(result.Buttons);
        Assert.Single(result.Buttons);
        Assert.Equal("/get_tokens", result.Buttons[0].Value);
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
        Assert.Contains($"Цена: {103.5m:F2} | Кол-во: 8", msg);
        Assert.Contains($"Цена: {98.02m:F2} | Кол-во: 2", msg);
        Assert.Contains($"💸 Баланс: {-828m:+0.00;-0.00}{Descriptor.CurrencySymbol}", msg);
        Assert.Contains($"💰 Баланс: {196.05m:+0.00;-0.00}{Descriptor.CurrencySymbol}", msg);

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
        Assert.Contains("Необходимо ввести положительное целое", result.Message);
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

    // ═══════════════════════════════════════════════════════════
    //  /admin_set_token_to_user
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminSetTokenToUser_ValidJson_Success()
    {
        _m.PortfolioUpdating
            .Setup(s => s.CreateOrUpdatePortfolioAsync(12345, "ARK_001", 100))
            .ReturnsAsync(Result.Ok());

        await _engine.ProcessInput(UserId, "/admin_set_token_to_user");
        var json = """{"traderId": 12345, "symbolId": "ARK_001", "quantity": 100}""";
        var result = await _engine.ProcessInput(UserId, json);

        Assert.NotNull(result.Message);
        Assert.Equal("Portfolia update successful", result.Message);
        _m.PortfolioUpdating.Verify(s => s.CreateOrUpdatePortfolioAsync(12345, "ARK_001", 100), Times.Once);
    }

    [Fact]
    public async Task AdminSetTokenToUser_InvalidJson_ReturnsError()
    {
        await _engine.ProcessInput(UserId, "/admin_set_token_to_user");
        var result = await _engine.ProcessInput(UserId, "not valid json");

        Assert.NotNull(result.Message);
        Assert.Contains("Error:", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  /admin_add_balance_to_user
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminAddBalanceToUser_ValidJson_Success()
    {
        _m.TraderBalanceUpdating
            .Setup(s => s.AddToBalanceAsync(12345, 500))
            .ReturnsAsync(Result.Ok());

        await _engine.ProcessInput(UserId, "/admin_add_balance_to_user");
        var json = """{"traderId": 12345, "amount": 500}""";
        var result = await _engine.ProcessInput(UserId, json);

        Assert.NotNull(result.Message);
        Assert.Equal("Balance update successful", result.Message);
        _m.TraderBalanceUpdating.Verify(s => s.AddToBalanceAsync(12345, 500), Times.Once);
    }

    [Fact]
    public async Task AdminAddBalanceToUser_InvalidJson_ReturnsError()
    {
        await _engine.ProcessInput(UserId, "/admin_add_balance_to_user");
        var result = await _engine.ProcessInput(UserId, "{broken json!!!");

        Assert.NotNull(result.Message);
        Assert.Contains("Error:", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  /admin_update_token_media
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminUpdateTokenMedia_ValidJson_Success()
    {
        _m.TokenMediaUpdate
            .Setup(s => s.UpdateTokenMediaAsync("ARK_001", "https://example.com/icon.png", "https://example.com/image.png"))
            .ReturnsAsync(Result.Ok());

        await _engine.ProcessInput(UserId, "/admin_update_token_media");
        var json = """{"symbol": "ARK_001", "iconUrl": "https://example.com/icon.png", "imageUrl": "https://example.com/image.png"}""";
        var result = await _engine.ProcessInput(UserId, json);

        Assert.NotNull(result.Message);
        Assert.Equal("Token media updated successfully", result.Message);
        _m.TokenMediaUpdate.Verify(s => s.UpdateTokenMediaAsync("ARK_001", "https://example.com/icon.png", "https://example.com/image.png"), Times.Once);
    }

    [Fact]
    public async Task AdminUpdateTokenMedia_InvalidJson_ReturnsError()
    {
        await _engine.ProcessInput(UserId, "/admin_update_token_media");
        var result = await _engine.ProcessInput(UserId, "12345");

        Assert.NotNull(result.Message);
        Assert.Contains("Error:", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  /admin_broadcast
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminBroadcast_SetMessage_MovesToConfirmStep()
    {
        var result = await _engine.ProcessInput(UserId, "/admin_broadcast");
        result = await _engine.ProcessInput(UserId, "Hello everyone!");

        Assert.NotNull(result.Message);
        Assert.Equal("Confirm broadcast? Reply 'confirm' to send or 'cancel' to abort.", result.Message);
    }

    [Fact]
    public async Task AdminBroadcast_Confirm_SendsToAllTraders()
    {
        _m.TraderQuery
            .Setup(s => s.GetAllTraderIdsAsync())
            .ReturnsAsync(Result<List<long>>.Ok(new List<long> { 100, 200 }));

        await _engine.ProcessInput(UserId, "/admin_broadcast");
        await _engine.ProcessInput(UserId, "Hello from admin!");
        var result = await _engine.ProcessInput(UserId, "confirm");

        Assert.NotNull(result.Message);
        Assert.Contains("Broadcast sent:", result.Message);
        Assert.Contains("delivered", result.Message);
        Assert.Contains("failed out of 2 total", result.Message);
        _m.MessageSender.Verify(s => s.SendMessageAsync(100, "Hello from admin!"), Times.Once);
        _m.MessageSender.Verify(s => s.SendMessageAsync(200, "Hello from admin!"), Times.Once);
    }

    [Fact]
    public async Task AdminBroadcast_Cancel_StopsBroadcast()
    {
        await _engine.ProcessInput(UserId, "/admin_broadcast");
        await _engine.ProcessInput(UserId, "This should be cancelled");
        var result = await _engine.ProcessInput(UserId, "cancel");

        Assert.NotNull(result.Message);
        Assert.Equal("Broadcast cancelled.", result.Message);
        _m.MessageSender.Verify(s => s.SendMessageAsync(It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AdminBroadcast_NoTraders_FailsGracefully()
    {
        _m.TraderQuery
            .Setup(s => s.GetAllTraderIdsAsync())
            .ReturnsAsync(Result<List<long>>.Fail("No traders found"));

        await _engine.ProcessInput(UserId, "/admin_broadcast");
        await _engine.ProcessInput(UserId, "Broadcast message");
        var result = await _engine.ProcessInput(UserId, "confirm");

        Assert.NotNull(result.Message);
        Assert.Contains("No traders found", result.Message);
    }

    #region AdminGetTraderProfile

    [Fact]
    public async Task AdminGetTraderProfile_ReturnsTraderProfileFile()
    {
        _m.TraderQuery
            .Setup(s => s.GetTraderProfileAsync(42))
            .ReturnsAsync(Result<TraderProfileInfo>.Ok(new TraderProfileInfo("testuser", 500m)));
        _m.BalanceSnapshot
            .Setup(s => s.TakeTotalTraderBalanceSnapshot(42))
            .ReturnsAsync(Result<BalanceSnapshotData>.Ok(
                new BalanceSnapshotData(42, 500m, 500m, 0m, 0m, 0m, DateTime.UtcNow)));
        _m.PortfolioQuery
            .Setup(s => s.GetTraderTokensAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<PortfolioItemInfo[]>.Ok(Array.Empty<PortfolioItemInfo>()));
        _m.LeadersTop
            .Setup(s => s.GetTraderPositionAsync(42))
            .ReturnsAsync(Result<LeaderPosition>.Ok(new LeaderPosition(7, 150, 0)));

        await _engine.ProcessInput(UserId, "/admin_get_trader_profile");
        var result = await _engine.ProcessInput(UserId, """{"telegramId": 42}""");

        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        Assert.Contains("trader_profile_42.txt", result.Message);
        Assert.NotNull(result.SentFilePath);
        Assert.True(File.Exists(result.SentFilePath));
        string content = File.ReadAllText(result.SentFilePath);
        Assert.Contains("=== Trader Profile: 42 ===", content);
        Assert.Contains("Username: testuser", content);
        Assert.Contains($"Balance: {500m:F2}", content);
        Assert.Contains("Total Balance:", content);
        Assert.Contains("Rank: #7 / 150", content);
        Assert.Contains("Portfolio: empty", content);
        _m.TraderQuery.Verify(s => s.GetTraderProfileAsync(42), Times.Once);
        _m.BalanceSnapshot.Verify(s => s.TakeTotalTraderBalanceSnapshot(42), Times.Once);
        _m.PortfolioQuery.Verify(s => s.GetTraderTokensAsync(42), Times.Once);
        _m.LeadersTop.Verify(s => s.GetTraderPositionAsync(42), Times.Once);
    }

    [Fact]
    public async Task AdminGetTraderProfile_TraderNotFound_ReturnsError()
    {
        _m.TraderQuery
            .Setup(s => s.GetTraderProfileAsync(999))
            .ReturnsAsync(Result<TraderProfileInfo>.Fail("Trader not found."));

        await _engine.ProcessInput(UserId, "/admin_get_trader_profile");
        var result = await _engine.ProcessInput(UserId, """{"telegramId": 999}""");

        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        Assert.Equal("Trader not found.", result.Message);
        _m.TraderQuery.Verify(s => s.GetTraderProfileAsync(999), Times.Once);
    }

    [Fact]
    public async Task AdminGetTraderProfile_MissingTelegramId_ReturnsError()
    {
        await _engine.ProcessInput(UserId, "/admin_get_trader_profile");
        var result = await _engine.ProcessInput(UserId, """{"foo": "bar"}""");

        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        Assert.Equal("Field \"telegramId\" is required.", result.Message);
    }

    [Fact]
    public async Task AdminGetTraderProfile_NegativeId_ReturnsError()
    {
        await _engine.ProcessInput(UserId, "/admin_get_trader_profile");
        var result = await _engine.ProcessInput(UserId, """{"telegramId": -5}""");

        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        Assert.Equal("telegramId must be a positive number.", result.Message);
    }

    [Fact]
    public async Task AdminGetTraderProfile_WithPortfolio_IncludesPortfolioDetails()
    {
        _m.TraderQuery
            .Setup(s => s.GetTraderProfileAsync(10))
            .ReturnsAsync(Result<TraderProfileInfo>.Ok(new TraderProfileInfo("Alice", 100m)));
        _m.BalanceSnapshot
            .Setup(s => s.TakeTotalTraderBalanceSnapshot(10))
            .ReturnsAsync(Result<BalanceSnapshotData>.Ok(
                new BalanceSnapshotData(10, 100m, 100m, 0m, 0m, 0m, DateTime.UtcNow)));
        _m.PortfolioQuery
            .Setup(s => s.GetTraderTokensAsync(10))
            .ReturnsAsync(Result<PortfolioItemInfo[]>.Ok(new[]
            {
                new PortfolioItemInfo(10m, 50m, 600m, 20m, new TokenInfo("TEST", "TestToken", 100m, "icon.png", "img.png"))
            }));
        _m.LeadersTop
            .Setup(s => s.GetTraderPositionAsync(10))
            .ReturnsAsync(Result<LeaderPosition>.Ok(new LeaderPosition(1, 50, 0)));

        await _engine.ProcessInput(UserId, "/admin_get_trader_profile");
        var result = await _engine.ProcessInput(UserId, """{"telegramId": 10}""");

        Assert.NotNull(result);
        Assert.True(result.SentFilePath != null && File.Exists(result.SentFilePath));
        string content = File.ReadAllText(result.SentFilePath);
        Assert.Contains("Portfolio:", content);
        Assert.Contains("TEST", content);
        Assert.Contains($"(avg: {50m:F2}", content);
        Assert.Contains($"current: {600m:F2}", content);
        Assert.Contains($"profit: {100m:+0.00;-0.00}", content);
    }

    #endregion

    #region AdminGetTraderOrders

    [Fact]
    public async Task AdminGetTraderOrders_ReturnsOrdersFile()
    {
        var ordersList = new List<OrderInfo>
        {
            new OrderInfo("1", "BTC/USDT", "BTC", "Buy", 1m, 0.5m, 50, 50000m, "Active")
        };
        _m.OrderQuery
            .Setup(s => s.GetTraderOrdersAsync(42, true, true, true, false))
            .ReturnsAsync(Result<List<OrderInfo>>.Ok(ordersList));

        await _engine.ProcessInput(UserId, "/admin_get_trader_orders");
        var result = await _engine.ProcessInput(UserId, """{"telegramId": 42}""");

        Assert.NotNull(result);
        Assert.NotNull(result.SentFilePath);
        Assert.True(File.Exists(result.SentFilePath));
        string content = File.ReadAllText(result.SentFilePath);
        Assert.Contains("=== Orders for Trader 42 ===", content);
        Assert.Contains("Filters: status=All, direction=All", content);
        Assert.Contains("Total: 1 orders", content);
        Assert.Contains("Order #1", content);
        Assert.Contains("Symbol: BTC/USDT | Direction: Buy", content);
        Assert.Contains($"Price: {50000m:F2}", content);
        Assert.Contains("Status: Active", content);
        _m.OrderQuery.Verify(s => s.GetTraderOrdersAsync(42, true, true, true, false), Times.Once);
    }

    [Fact]
    public async Task AdminGetTraderOrders_DirectionFilter_BuyOnly()
    {
        var allOrders = new List<OrderInfo>
        {
            new OrderInfo("1", "BTC/USDT", "BTC", "Buy", 1m, 0m, 0m, 50000m, "Active"),
            new OrderInfo("2", "ETH/USDT", "ETH", "Sell", 2m, 0m, 0m, 3000m, "Active")
        };
        _m.OrderQuery
            .Setup(s => s.GetTraderOrdersAsync(42, true, true, true, false))
            .ReturnsAsync(Result<List<OrderInfo>>.Ok(allOrders));

        await _engine.ProcessInput(UserId, "/admin_get_trader_orders");
        var result = await _engine.ProcessInput(UserId, """{"telegramId": 42, "direction": "Buy"}""");

        Assert.NotNull(result);
        string content = File.ReadAllText(result.SentFilePath!);
        Assert.Contains("Direction: Buy", content);
        Assert.DoesNotContain("Direction: Sell", content);
    }

    [Fact]
    public async Task AdminGetTraderOrders_NoOrders_ReturnsEmpty()
    {
        _m.OrderQuery
            .Setup(s => s.GetTraderOrdersAsync(42, true, true, true, false))
            .ReturnsAsync(Result<List<OrderInfo>>.Ok(new List<OrderInfo>()));

        await _engine.ProcessInput(UserId, "/admin_get_trader_orders");
        var result = await _engine.ProcessInput(UserId, """{"telegramId": 42}""");

        Assert.NotNull(result);
        string content = File.ReadAllText(result.SentFilePath!);
        Assert.Contains("No orders found.", content);
    }

    [Fact]
    public async Task AdminGetTraderOrders_TraderNotFound_ReturnsError()
    {
        _m.OrderQuery
            .Setup(s => s.GetTraderOrdersAsync(999, true, true, true, false))
            .ReturnsAsync(Result<List<OrderInfo>>.Fail("Failed to get orders."));

        await _engine.ProcessInput(UserId, "/admin_get_trader_orders");
        var result = await _engine.ProcessInput(UserId, """{"telegramId": 999}""");

        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        Assert.Equal("Failed to get orders.", result.Message);
    }

    [Fact]
    public async Task AdminGetTraderOrders_MissingTelegramId_ReturnsError()
    {
        await _engine.ProcessInput(UserId, "/admin_get_trader_orders");
        var result = await _engine.ProcessInput(UserId, """{"foo": "bar"}""");

        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        Assert.Equal("Field \"telegramId\" is required.", result.Message);
    }

    #endregion

    #region AdminGetTraderTrades

    [Fact]
    public async Task AdminGetTraderTrades_ReturnsTradesFile()
    {
        var tradesList = new List<TradeInfo>
        {
            new TradeInfo("Buyer", 100m, 5m, 25m, new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc), new TokenInfo("BTC", "Bitcoin", 100m, "icon.png", "img.png"))
        };
        _m.TradeQuery
            .Setup(s => s.GetTraderTradesAsync(42, true))
            .ReturnsAsync(Result<List<TradeInfo>>.Ok(tradesList));

        await _engine.ProcessInput(UserId, "/admin_get_trader_trades");
        var result = await _engine.ProcessInput(UserId, """{"telegramId": 42}""");

        Assert.NotNull(result);
        Assert.NotNull(result.SentFilePath);
        Assert.True(File.Exists(result.SentFilePath));
        string content = File.ReadAllText(result.SentFilePath);
        Assert.Contains("=== Trades for Trader 42 ===", content);
        Assert.Contains("Filter: direction=All", content);
        Assert.Contains("Total: 1 trades", content);
        Assert.Contains("Buyer BTC", content);
        Assert.Contains($"Price: {100m:F2}", content);
        Assert.Contains("Qty: 5", content);
        Assert.Contains($"PnL: {25m:+0.00;-0.00}", content);
        _m.TradeQuery.Verify(s => s.GetTraderTradesAsync(42, true), Times.Once);
    }

    [Fact]
    public async Task AdminGetTraderTrades_BuyFilter_ShowsOnlyBuyers()
    {
        var tradesList = new List<TradeInfo>
        {
            new TradeInfo("Buyer", 10m, 1m, 0m, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), new TokenInfo("A", "A", 10m, "", "")),
            new TradeInfo("Seller", 10m, 1m, 0m, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), new TokenInfo("B", "B", 10m, "", ""))
        };
        _m.TradeQuery
            .Setup(s => s.GetTraderTradesAsync(42, true))
            .ReturnsAsync(Result<List<TradeInfo>>.Ok(tradesList));

        await _engine.ProcessInput(UserId, "/admin_get_trader_trades");
        var result = await _engine.ProcessInput(UserId, """{"telegramId": 42, "direction": "Buy"}""");

        Assert.NotNull(result);
        string content = File.ReadAllText(result.SentFilePath!);
        Assert.Contains("Buyer A", content);
        Assert.DoesNotContain("Seller B", content);
    }

    [Fact]
    public async Task AdminGetTraderTrades_NoTrades_ReturnsEmpty()
    {
        _m.TradeQuery
            .Setup(s => s.GetTraderTradesAsync(42, true))
            .ReturnsAsync(Result<List<TradeInfo>>.Ok(new List<TradeInfo>()));

        await _engine.ProcessInput(UserId, "/admin_get_trader_trades");
        var result = await _engine.ProcessInput(UserId, """{"telegramId": 42}""");

        Assert.NotNull(result);
        string content = File.ReadAllText(result.SentFilePath!);
        Assert.Contains("No trades found.", content);
    }

    [Fact]
    public async Task AdminGetTraderTrades_MissingTelegramId_ReturnsError()
    {
        await _engine.ProcessInput(UserId, "/admin_get_trader_trades");
        var result = await _engine.ProcessInput(UserId, """{}""");

        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        Assert.Equal("Field \"telegramId\" is required.", result.Message);
    }

    [Fact]
    public async Task AdminGetTraderTrades_FailedFetch_ReturnsErrorMessage()
    {
        _m.TradeQuery
            .Setup(s => s.GetTraderTradesAsync(999, true))
            .ReturnsAsync(Result<List<TradeInfo>>.Fail("Failed to get trades."));

        await _engine.ProcessInput(UserId, "/admin_get_trader_trades");
        var result = await _engine.ProcessInput(UserId, """{"telegramId": 999}""");

        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        Assert.Equal("Failed to get trades.", result.Message);
    }

    #endregion

    #region AdminGetTraderPortfolio

    [Fact]
    public async Task AdminGetTraderPortfolio_ReturnsPortfolioFile()
    {
        _m.PortfolioQuery
            .Setup(s => s.GetTraderTokensAsync(42))
            .ReturnsAsync(Result<PortfolioItemInfo[]>.Ok(new[]
            {
                new PortfolioItemInfo(10m, 50m, 700m, 40m, new TokenInfo("TEST", "TestCoin", 80m, "icon.png", "img.png"))
            }));

        await _engine.ProcessInput(UserId, "/admin_get_trader_portfolio");
        var result = await _engine.ProcessInput(UserId, """{"telegramId": 42}""");

        Assert.NotNull(result);
        Assert.NotNull(result.SentFilePath);
        Assert.True(File.Exists(result.SentFilePath));
        string content = File.ReadAllText(result.SentFilePath);
        Assert.Contains("=== Portfolio for Trader 42 ===", content);
        Assert.Contains("Filter: symbol=All", content);
        Assert.Contains("Total: 1 tokens", content);
        Assert.Contains("TEST (TestCoin)", content);
        Assert.Contains("Quantity: 10", content);
        Assert.Contains($"Avg Buy Price: {50m:F2}", content);
        Assert.Contains($"Current Value: {700m:F2}", content);
        Assert.Contains($"Profit: {40m:+0.00;-0.00}%", content);
        _m.PortfolioQuery.Verify(s => s.GetTraderTokensAsync(42), Times.Once);
    }

    [Fact]
    public async Task AdminGetTraderPortfolio_SymbolFilter_FilteredCorrectly()
    {
        _m.PortfolioQuery
            .Setup(s => s.GetTraderTokensAsync(42))
            .ReturnsAsync(Result<PortfolioItemInfo[]>.Ok(new[]
            {
                new PortfolioItemInfo(1m, 100m, 200m, 100m, new TokenInfo("BTC", "Bitcoin", 100m, "", "")),
                new PortfolioItemInfo(2m, 50m, 100m, 0m, new TokenInfo("ETH", "Ethereum", 50m, "", ""))
            }));

        await _engine.ProcessInput(UserId, "/admin_get_trader_portfolio");
        var result = await _engine.ProcessInput(UserId, """{"telegramId": 42, "symbol": "btc"}""");

        Assert.NotNull(result);
        string content = File.ReadAllText(result.SentFilePath!);
        Assert.Contains("Filter: symbol=btc", content);
        Assert.Contains("BTC", content);
        Assert.DoesNotContain("ETH", content);
    }

    [Fact]
    public async Task AdminGetTraderPortfolio_EmptyPortfolio_ReturnsEmptyMessage()
    {
        _m.PortfolioQuery
            .Setup(s => s.GetTraderTokensAsync(42))
            .ReturnsAsync(Result<PortfolioItemInfo[]>.Ok(Array.Empty<PortfolioItemInfo>()));

        await _engine.ProcessInput(UserId, "/admin_get_trader_portfolio");
        var result = await _engine.ProcessInput(UserId, """{"telegramId": 42}""");

        Assert.NotNull(result);
        string content = File.ReadAllText(result.SentFilePath!);
        Assert.Contains("Portfolio is empty.", content);
    }

    [Fact]
    public async Task AdminGetTraderPortfolio_MissingTelegramId_ReturnsError()
    {
        await _engine.ProcessInput(UserId, "/admin_get_trader_portfolio");
        var result = await _engine.ProcessInput(UserId, """{}""");

        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        Assert.Equal("Field \"telegramId\" is required.", result.Message);
    }

    [Fact]
    public async Task AdminGetTraderPortfolio_PortfolioFetchFail_ReturnsError()
    {
        _m.PortfolioQuery
            .Setup(s => s.GetTraderTokensAsync(999))
            .ReturnsAsync(Result<PortfolioItemInfo[]>.Fail("Failed to get portfolio."));

        await _engine.ProcessInput(UserId, "/admin_get_trader_portfolio");
        var result = await _engine.ProcessInput(UserId, """{"telegramId": 999}""");

        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        Assert.Equal("Failed to get portfolio.", result.Message);
    }

    #endregion

    #region AdminMetrics

    [Fact]
    public async Task AdminMetrics_ReturnsMetricsFile()
    {
        _m.MetricsSnapshot
            .Setup(s => s.GetMetricsTextAsync())
            .ReturnsAsync("system_up 1\ncpu_usage 42\n");

        var result = await _engine.ProcessInput(UserId, "/admin_metrics");

        Assert.NotNull(result);
        Assert.NotNull(result.SentFilePath);
        Assert.True(File.Exists(result.SentFilePath));
        string content = File.ReadAllText(result.SentFilePath);
        Assert.Contains("=== ArkWallet Metrics ===", content);
        Assert.Contains("system_up 1", content);
        Assert.Contains("cpu_usage 42", content);
        Assert.Contains("/metrics (порт 5000)", content);
        _m.MetricsSnapshot.Verify(s => s.GetMetricsTextAsync(), Times.Once);
    }

    [Fact]
    public async Task AdminMetrics_FetchError_ReturnsError()
    {
        _m.MetricsSnapshot
            .Setup(s => s.GetMetricsTextAsync())
            .Throws(new InvalidOperationException("Prometheus unreachable"));

        var result = await _engine.ProcessInput(UserId, "/admin_metrics");

        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        Assert.Equal("Error: Prometheus unreachable", result.Message);
    }

    #endregion

    #region AdminSendMail

    [Fact]
    public async Task AdminSendMail_SingleRecipient_CreatesMailMessage()
    {
        _m.MailMessage
            .Setup(s => s.CreateManyAsync(It.IsAny<IReadOnlyList<CreateCommand>>()))
            .ReturnsAsync(Result<List<MailCreateResult>>.Ok(new List<MailCreateResult> { new(1) }));

        await _engine.ProcessInput(UserId, "/admin_send_mail");
        var result = await _engine.ProcessInput(UserId,
            """{"recipientId": 100, "title": "Hello", "message": "World", "rewardSymbol": "TKN", "rewardAmount": 5.0}""");

        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        Assert.Contains("✉️ Письма отправлены: 1 пользователям", result.Message!);
        Assert.Contains("🎁 Награда:", result.Message!);
        Assert.Contains("TKN", result.Message!);
        _m.MailMessage.Verify(s => s.CreateManyAsync(It.Is<IReadOnlyList<CreateCommand>>(
            cmds => cmds.Count == 1 && cmds[0].TraderId == 100)), Times.Once);
    }

    [Fact]
    public async Task AdminSendMail_AllRecipients_QueriesAndSendsToAll()
    {
        _m.TraderQuery
            .Setup(s => s.GetAllTraderIdsAsync())
            .ReturnsAsync(Result<List<long>>.Ok(new List<long> { 1, 2, 3 }));
        _m.MailMessage
            .Setup(s => s.CreateManyAsync(It.IsAny<IReadOnlyList<CreateCommand>>()))
            .ReturnsAsync(Result<List<MailCreateResult>>.Ok(new List<MailCreateResult> { new(1), new(2), new(3) }));

        await _engine.ProcessInput(UserId, "/admin_send_mail");
        var result = await _engine.ProcessInput(UserId,
            """{"recipientId": "all", "title": "Update", "message": "New feature released", "rewardSymbol": "", "rewardAmount": 0}""");

        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        Assert.Contains("✉️ Письма отправлены: 3 пользователям", result.Message);
        Assert.DoesNotContain("Награда", result.Message);
    }

    [Fact]
    public async Task AdminSendMail_MissingFields_ReturnsError()
    {
        await _engine.ProcessInput(UserId, "/admin_send_mail");
        var result = await _engine.ProcessInput(UserId,
            """{"recipientId": 100, "title": "", "message": ""}""");

        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        Assert.Equal("title и message не могут быть пустыми.", result.Message);
    }

    [Fact]
    public async Task AdminSendMail_NoRecipientId_ReturnsError()
    {
        await _engine.ProcessInput(UserId, "/admin_send_mail");
        var result = await _engine.ProcessInput(UserId,
            """{"title": "Hello", "message": "World"}""");

        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        Assert.Equal("recipientId обязателен.", result.Message);
    }

    [Fact]
    public async Task AdminSendMail_ArrayRecipients_AllIncluded()
    {
        _m.MailMessage
            .Setup(s => s.CreateManyAsync(It.IsAny<IReadOnlyList<CreateCommand>>()))
            .ReturnsAsync(Result<List<MailCreateResult>>.Ok(new List<MailCreateResult> { new(1), new(2) }));

        await _engine.ProcessInput(UserId, "/admin_send_mail");
        var result = await _engine.ProcessInput(UserId,
            """{"recipientId": [10, 20], "title": "Multi", "message": "Send", "rewardSymbol": "", "rewardAmount": 0}""");

        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        Assert.Contains("✉️ Письма отправлены: 2 пользователям", result.Message);
    }

    [Fact]
    public async Task AdminSendMail_InvalidRecipientType_ReturnsError()
    {
        await _engine.ProcessInput(UserId, "/admin_send_mail");
        var result = await _engine.ProcessInput(UserId,
            """{"recipientId": "invalid", "title": "T", "message": "M"}""");

        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        Assert.Equal("recipientId должен быть Telegram ID, массивом ID или \"all\".", result.Message);
    }

    [Fact]
    public async Task AdminSendMail_AllButNoTraders_ReturnsError()
    {
        _m.TraderQuery
            .Setup(s => s.GetAllTraderIdsAsync())
            .ReturnsAsync(Result<List<long>>.Fail("Нет пользователей"));

        await _engine.ProcessInput(UserId, "/admin_send_mail");
        var result = await _engine.ProcessInput(UserId,
            """{"recipientId": "all", "title": "T", "message": "M"}""");

        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        Assert.Equal("Нет зарегистрированных пользователей для рассылки.", result.Message);
    }

    #endregion
}
