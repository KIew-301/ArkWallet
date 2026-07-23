using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Decorators;
using ArkWallet.Application.Contracts.Leaders;
using ArkWallet.Application.Contracts.Orchestrators;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Contracts.TradeServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Entities.Configurations;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Infrastructure.Wizard
{
    [ExcludeFromCodeCoverage(Justification = "UI-оркестратор Telegram-бота, управляет сессиями пользователей. Зависит от внешнего Telegram API, тестируется интеграционно.")]
    internal partial class WizardEngine
    {
        private const string PlaceOrderCommand = "/place_order";
        private const string ServerErrorMessage = "Ошибка на стороне сервера";

        private readonly WizardConfiguration _config;
        private readonly IUserSessionStore _sessionStore;
        private readonly ILogger<WizardEngine> _logger;

        // TRADER SERVICES
        private readonly ITraderRegistrationService _traderRegistrationService;
        private readonly ITraderBalanceUpdatingService _traderBalanceUpdatingService;
        private readonly ITraderQueryService _traderQueryService;

        // ORDER SERVICES
        private readonly IOrderValidationService _orderValidationService;
        private readonly IOrderCreationService _orderCreationService;
        private readonly IOrderCancellationService _cancelOrderService;
        private readonly IOrderBookService _orderBookService;
        private readonly IOrderQueryService _orderQueryService;

        // PORTFOLIO & TOKEN SERVICES
        private readonly IPortfolioQueryService _portfolioQueryService;
        private readonly IPortfolioUpdatingService _portfolioUpdatingService;
        private readonly ITokenCreationService _tokenCreationServices;
        private readonly ITokenQueryService _tokenQueryService;
        private readonly ITokenMediaUpdateService _tokenMediaUpdateService;

        // TRADE SERVICES
        private readonly ITradeQueryService _tradeQueryService;

        // LEADERS
        private readonly ILeadersTopByBalanceQueryService _leadersTopByBalanceQueryService;
        private readonly IBalanceSnapshotService _balanceSnapshotService;

        // ORCHESTRATORS
        private readonly ICandleOrchestrator _candleOrchestrator;

        // DECORATOR SERVICES
        private readonly IQuestionDecorator _questionDecorator;
        private readonly IButtonDecorator _buttonDecorator;

        public WizardEngine(
            IUserSessionStore sessionStore,
            ILogger<WizardEngine> logger,
            ITraderRegistrationService traderRegistrationService,
            ITraderBalanceUpdatingService traderBalanceUpdatingService,
            ITraderQueryService traderQueryService,
            IOrderValidationService orderValidationService,
            IOrderCreationService orderCreationService,
            IOrderCancellationService cancelOrderService,
            IOrderBookService orderBookService,
            IOrderQueryService orderQueryService,
            IPortfolioQueryService portfolioQueryService,
            IPortfolioUpdatingService portfolioUpdatingService,
            ITokenCreationService tokenCreationServices,
            ITokenQueryService tokenQueryService,
            ITokenMediaUpdateService tokenMediaUpdateService,
            ITradeQueryService tradeQueryService,
            ILeadersTopByBalanceQueryService leadersTopByBalanceQueryService,
            IBalanceSnapshotService balanceSnapshotService,
            ICandleOrchestrator candleOrchestrator,
            IQuestionDecorator questionDecorator,
            IButtonDecorator buttonDecorator,
            WizardConfiguration config
            )
        {
            _sessionStore = sessionStore;
            _logger = logger;
            _traderRegistrationService = traderRegistrationService;
            _traderBalanceUpdatingService = traderBalanceUpdatingService;
            _traderQueryService = traderQueryService;
            _orderValidationService = orderValidationService;
            _orderCreationService = orderCreationService;
            _cancelOrderService = cancelOrderService;
            _orderBookService = orderBookService;
            _orderQueryService = orderQueryService;
            _portfolioQueryService = portfolioQueryService;
            _portfolioUpdatingService = portfolioUpdatingService;
            _tokenCreationServices = tokenCreationServices;
            _tokenQueryService = tokenQueryService;
            _tokenMediaUpdateService = tokenMediaUpdateService;
            _tradeQueryService = tradeQueryService;
            _leadersTopByBalanceQueryService = leadersTopByBalanceQueryService;
            _balanceSnapshotService = balanceSnapshotService;
            _candleOrchestrator = candleOrchestrator;
            _questionDecorator = questionDecorator;
            _buttonDecorator = buttonDecorator;
            _config = config;

            ConfigureHandlers();
            ConfigureAdditionHandlers();
        }

        private void ConfigureHandlers()
        {
            // Регистрация комманд
            _config.Commands["/start"][0].Handler = HandleSetName;
            _config.Commands[PlaceOrderCommand][0].Handler = HandleSetDirection;
            _config.Commands[PlaceOrderCommand][1].Handler = HandleSetToken;
            _config.Commands[PlaceOrderCommand][2].Handler = HandleSetTokenQuantity;
            _config.Commands[PlaceOrderCommand][3].Handler = HandleSetTokenPrice;
            _config.Commands["/cancel_order"][0].Handler = HandleSelectOrderToCancel;
            _config.Commands["/cancel_order"][1].Handler = HandleConfirmCancellation;
            _config.Commands["/cancel_all_orders"][0].Handler = HandleConfirmCancellationAllOrders;
            _config.Commands["/get_profile"][0].Handler = HandleGetProfile;
            _config.Commands["/get_token_info"][0].Handler = HandleSelectTokenInfo;
            _config.Commands["/get_token_info"][1].Handler = HandleShowTokenInfo;
            _config.Commands["/get_price_history"][0].Handler = HandleSelectTokenForHistory;
            _config.Commands["/get_price_history"][1].Handler = HandleSetTimeframe;
            _config.Commands["/get_price_history"][2].Handler = HandleSetLimit;
            _config.Commands["/get_order_book"][0].Handler = HandleSelectTokenForOrderBook;
            _config.Commands["/get_order_book"][1].Handler = HandleSetBuyCount;
            _config.Commands["/get_order_book"][2].Handler = HandleSetSellCount;
            _config.Commands["/get_orders"][0].Handler = HandleGetOrders;
            _config.Commands["/get_trades"][0].Handler = HandleSetTradesLimit;
            _config.Commands["/get_tops"][0].Handler = HandleSetTopsLimit;
        }

        public async Task<(string? message, List<QuickButton>?)> ProcessInput(long userId, string input)
        {
            try
            {
                if (input.StartsWith("/get_order_book "))
                {
                    var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 4)
                    {
                        return await HandleQuickOrderBook(parts[1], parts[2], parts[3]);
                    }
                }

                if (input.StartsWith("/get_trades "))
                {
                    var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        return await HandleQuickTrades(userId, parts[1]);
                    }
                }

                if (_config.Commands.ContainsKey(input))
                {
                    return await StartCommand(userId, input);
                }

                if (_sessionStore.TryGet(userId, out var session) && session != null)
                {
                    return await ContinueCommand(userId, input, session);
                }

                return ("Неизвестная команда", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wizard ProcessInput failed for user {UserId}, input: {Input}", userId, input);
                return (ServerErrorMessage, null);
            }
        }

        private async Task<(string?, List<QuickButton>?)> StartCommand(long userId, string command)
        {
            if (command is "/cancel_order" or "/cancel_all_orders")
            {
                var hasActiveOrders = await _cancelOrderService.HasActiveOrdersAsync(userId);
                if (!hasActiveOrders)
                    return ("Нет активных ордеров для отмены.", null);
            }

            if (command == "/start")
            {
                var isRegistered = await _traderRegistrationService.CheckTraderAlreadyRegistered(userId);
                if (isRegistered)
                    return ("Вы уже зарегистрированы! Используйте /get_profile для просмотра профиля.", null);
            }

            var session = new UserSession
            {
                Id = userId,
                CurrentCommand = command,
                CurrentStep = _config.Commands[command].First().Name
            };

            var commandSteps = _config.Commands[session.CurrentCommand];
            var currentStep = commandSteps.First(s => s.Name == session.CurrentStep);

            if (!currentStep.OneStep)
            {
                _sessionStore.Set(userId, session);

                var nextStep = commandSteps.First(s => s.Name == session.CurrentStep);

                var question = await _questionDecorator.DecorateQuestionAsync(nextStep.Name, nextStep.Question, session);
                var buttons = await _buttonDecorator.DecorateButtonsAsync(nextStep.Name, nextStep.Buttons, session);

                var step = _config.Commands[command].First();
                return (question, buttons);
            }
            else
            {
                var result = await currentStep.Handler(session, command);

                if (!result.Success)
                {
                    _logger.LogWarning("Wizard OneStep handler error for user {UserId}, command {Command}: {Error}",
                        userId, command, result.Message);
                    return (ServerErrorMessage, null);
                }

                return (result.Message ?? "Готово!", result.Buttons);
            }
        }

        private async Task<(string?, List<QuickButton>?)> ContinueCommand(long userId, string input, UserSession session)
        {
            var commandSteps = _config.Commands[session.CurrentCommand];
            var currentStep = commandSteps.First(s => s.Name == session.CurrentStep);

            // Выполняем handler
            var result = await currentStep.Handler(session, input);

            if (!result.Success)
            {
                _logger.LogWarning("Wizard step error for user {UserId}, command {Command}, step {Step}: {Error}",
                    userId, session.CurrentCommand, session.CurrentStep, result.Message);
                return (ServerErrorMessage, currentStep.Buttons);
            }

            // Успех - переходим к следующему шагу
            if (result.NextStep == "completed")
            {
                _sessionStore.Remove(userId);
                return (result.Message ?? "Готово!", result.Buttons);
            }

            // Обновляем шаг и возвращаем следующий вопрос
            session.CurrentStep = result.NextStep;
            var nextStep = commandSteps.First(s => s.Name == result.NextStep);

            if (nextStep.OneStep)
            {
                _sessionStore.Remove(userId);
                if (nextStep.Handler == null)
                {
                    _logger.LogError("Handler not found for step {Step} in command {Command}", result.NextStep, session.CurrentCommand);
                    return (ServerErrorMessage, null);
                }

                var oneStepResult = await nextStep.Handler(session, input);

                if (!oneStepResult.Success)
                {
                    _logger.LogWarning("Wizard OneStep handler error for user {UserId}, command {Command}, step {Step}: {Error}",
                        userId, session.CurrentCommand, result.NextStep, oneStepResult.Message);
                    return (ServerErrorMessage, null);
                }

                return (oneStepResult.Message ?? "Готово!", oneStepResult.Buttons);
            }

            var question = await _questionDecorator.DecorateQuestionAsync(nextStep.Name, nextStep.Question, session);
            var buttons = await _buttonDecorator.DecorateButtonsAsync(nextStep.Name, nextStep.Buttons, session);

            return (question, buttons);
        }
    }
}
