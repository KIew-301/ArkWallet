using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Decorators;
using ArkWallet.Application.Contracts.Leaders;
using ArkWallet.Application.Contracts.MarketMaker;
using ArkWallet.Application.Contracts.Orchestrators;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.SuggestionServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Contracts.TradeServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Application.Services.Wizard;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Entities.Configurations;
using ArkWallet.Infrastructure.Wizard;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.IntegrationTests;

internal static class WizardEngineTestHelper
{
    public static ServiceMocks Build()
    {
        var sessionStore = new UserSessionStore();
        var config = new WizardConfiguration();

        var traderRegistrationService = new Mock<ITraderRegistrationService>();
        var traderBalanceUpdatingService = new Mock<ITraderBalanceUpdatingService>();
        var traderQueryService = new Mock<ITraderQueryService>();

        var orderValidationService = new Mock<IOrderValidationService>();
        var orderCreationService = new Mock<IOrderCreationService>();
        var orderCancellationService = new Mock<IOrderCancellationService>();
        var orderBookService = new Mock<IOrderBookService>();
        var orderQueryService = new Mock<IOrderQueryService>();

        var portfolioQueryService = new Mock<IPortfolioQueryService>();
        var portfolioUpdatingService = new Mock<IPortfolioUpdatingService>();

        var tokenCreationService = new Mock<ITokenCreationService>();
        var tokenQueryService = new Mock<ITokenQueryService>();
        var tokenMediaUpdateService = new Mock<ITokenMediaUpdateService>();
        var tokenDeletionService = new Mock<ITokenDeletionService>();

        var tradeQueryService = new Mock<ITradeQueryService>();

        var leadersTopByBalanceQueryService = new Mock<ILeadersTopByBalanceQueryService>();
        var balanceSnapshotService = new Mock<IBalanceSnapshotService>();

        var candleOrchestrator = new Mock<ICandleOrchestrator>();
        var botQueryService = new Mock<IMarketMakerBotQueryService>();
        var tokenService = new Mock<ITokenService>();
        var tradingVolumeService = new Mock<ITradingVolumeService>();
        var messageSender = new Mock<IMessageSender>();
        var metricsSnapshotService = new Mock<IMetricsSnapshotService>();

        var configuration = new Mock<IConfiguration>();
        configuration.Setup(c => c["Telegram:AdminId:Main"]).Returns("999999");

        var questionDecorator = new Mock<IQuestionDecorator>();
        questionDecorator
            .Setup(d => d.DecorateQuestionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<UserSession>()))
            .ReturnsAsync((string _, string baseQ, UserSession _) => baseQ);

        var buttonDecorator = new Mock<IButtonDecorator>();
        buttonDecorator
            .Setup(d => d.DecorateButtonsAsync(It.IsAny<string>(), It.IsAny<List<QuickButton>>(), It.IsAny<UserSession>()))
            .ReturnsAsync((string _, List<QuickButton> baseB, UserSession _) => baseB);

        var engine = new WizardEngine(
            sessionStore,
            NullLogger<WizardEngine>.Instance,
            traderRegistrationService.Object,
            traderBalanceUpdatingService.Object,
            traderQueryService.Object,
            orderValidationService.Object,
            orderCreationService.Object,
            orderCancellationService.Object,
            orderBookService.Object,
            orderQueryService.Object,
            portfolioQueryService.Object,
            portfolioUpdatingService.Object,
            tokenCreationService.Object,
            tokenQueryService.Object,
            tokenMediaUpdateService.Object,
            tokenDeletionService.Object,
            tradeQueryService.Object,
            leadersTopByBalanceQueryService.Object,
            balanceSnapshotService.Object,
            candleOrchestrator.Object,
            botQueryService.Object,
            tokenService.Object,
            tradingVolumeService.Object,
            messageSender.Object,
            configuration.Object,
            questionDecorator.Object,
            buttonDecorator.Object,
            metricsSnapshotService.Object,
            config
        );

        return new ServiceMocks
        {
            Engine = engine,
            TraderRegistration = traderRegistrationService,
            TraderBalanceUpdating = traderBalanceUpdatingService,
            TraderQuery = traderQueryService,
            OrderValidation = orderValidationService,
            OrderCreation = orderCreationService,
            OrderCancellation = orderCancellationService,
            OrderBook = orderBookService,
            OrderQuery = orderQueryService,
            PortfolioQuery = portfolioQueryService,
            PortfolioUpdating = portfolioUpdatingService,
            TokenCreation = tokenCreationService,
            TokenQuery = tokenQueryService,
            TokenMediaUpdate = tokenMediaUpdateService,
            TokenDeletion = tokenDeletionService,
            TradeQuery = tradeQueryService,
            LeadersTop = leadersTopByBalanceQueryService,
            BalanceSnapshot = balanceSnapshotService,
            CandleOrchestrator = candleOrchestrator,
            BotQuery = botQueryService,
            TokenService = tokenService,
            TradingVolume = tradingVolumeService,
            MessageSender = messageSender,
            Configuration = configuration
        };
    }
}

internal class ServiceMocks
{
    public WizardEngine Engine { get; init; } = null!;
    public Mock<ITraderRegistrationService> TraderRegistration { get; init; } = null!;
    public Mock<ITraderBalanceUpdatingService> TraderBalanceUpdating { get; init; } = null!;
    public Mock<ITraderQueryService> TraderQuery { get; init; } = null!;
    public Mock<IOrderValidationService> OrderValidation { get; init; } = null!;
    public Mock<IOrderCreationService> OrderCreation { get; init; } = null!;
    public Mock<IOrderCancellationService> OrderCancellation { get; init; } = null!;
    public Mock<IOrderBookService> OrderBook { get; init; } = null!;
    public Mock<IOrderQueryService> OrderQuery { get; init; } = null!;
    public Mock<IPortfolioQueryService> PortfolioQuery { get; init; } = null!;
    public Mock<IPortfolioUpdatingService> PortfolioUpdating { get; init; } = null!;
    public Mock<ITokenCreationService> TokenCreation { get; init; } = null!;
    public Mock<ITokenQueryService> TokenQuery { get; init; } = null!;
    public Mock<ITokenMediaUpdateService> TokenMediaUpdate { get; init; } = null!;
    public Mock<ITokenDeletionService> TokenDeletion { get; init; } = null!;
    public Mock<ITradeQueryService> TradeQuery { get; init; } = null!;
    public Mock<ILeadersTopByBalanceQueryService> LeadersTop { get; init; } = null!;
    public Mock<IBalanceSnapshotService> BalanceSnapshot { get; init; } = null!;
    public Mock<ICandleOrchestrator> CandleOrchestrator { get; init; } = null!;
    public Mock<IMarketMakerBotQueryService> BotQuery { get; init; } = null!;
    public Mock<ITokenService> TokenService { get; init; } = null!;
    public Mock<ITradingVolumeService> TradingVolume { get; init; } = null!;
    public Mock<IMessageSender> MessageSender { get; init; } = null!;
    public Mock<IConfiguration> Configuration { get; init; } = null!;
}
