using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Decorators;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Entities.Configurations;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Infrastructure.Wizard
{
    [ExcludeFromCodeCoverage(Justification = "UI-оркестратор Telegram-бота, управляет сессиями пользователей. Зависит от внешнего Telegram API, тестируется интеграционно.")]
    internal partial class WizardEngine
    {
        private readonly WizardConfiguration _config;
        private readonly UserSessionStore _sessionStore;

        // TRADER SERVICES
        private readonly ITraderRegistrationService _traderRegistrationService;
        private readonly ITraderBalanceUpdatingService _traderBalanceUpdatingService;
        private readonly ITraderQueryService _traderQueryService;

        // ORDER SERVICES
        private readonly IOrderValidationService _orderValidationService;
        private readonly IOrderCreationService _orderCreationService;
        private readonly IOrderCancellationService _cancelOrderService;

        // PORTFOLIO & TOKEN SERVICES
        private readonly IPortfolioQueryService _portfolioQueryService;
        private readonly IPortfolioUpdatingService _portfolioUpdatingService;
        private readonly ITokenCreationService _tokenCreationServices;
        private readonly ITokenQueryService _tokenQueryService;
        private readonly ITokenMediaUpdateService _tokenMediaUpdateService;

        // DECORATOR SERVICES
        private readonly IQuestionDecorator _questionDecorator;
        private readonly IButtonDecorator _buttonDecorator;

        public WizardEngine(
            ITraderRegistrationService traderRegistrationService,
            ITraderBalanceUpdatingService traderBalanceUpdatingService,
            ITraderQueryService traderQueryService,
            IOrderValidationService orderValidationService,
            IOrderCreationService orderCreationService,
            IOrderCancellationService cancelOrderService,
            IPortfolioQueryService portfolioQueryService,
            IPortfolioUpdatingService portfolioUpdatingService,
            ITokenCreationService tokenCreationServices,
            ITokenQueryService tokenQueryService,
            ITokenMediaUpdateService tokenMediaUpdateService,
            IQuestionDecorator questionDecorator,
            IButtonDecorator buttonDecorator,
            WizardConfiguration config,
            UserSessionStore sessionStore
            )
        {
            _traderRegistrationService = traderRegistrationService;
            _traderBalanceUpdatingService = traderBalanceUpdatingService;
            _traderQueryService = traderQueryService;
            _orderValidationService = orderValidationService;
            _orderCreationService = orderCreationService;
            _cancelOrderService = cancelOrderService;
            _portfolioQueryService = portfolioQueryService;
            _portfolioUpdatingService = portfolioUpdatingService;
            _tokenCreationServices = tokenCreationServices;
            _tokenQueryService = tokenQueryService;
            _tokenMediaUpdateService = tokenMediaUpdateService;
            _questionDecorator = questionDecorator;
            _buttonDecorator = buttonDecorator;
            _config = config;
            _sessionStore = sessionStore;

            ConfigureHandlers();
            ConfigureAdditionHandlers();
        }

        private void ConfigureHandlers()
        {
            // Регистрация комманд
            _config.Commands["/start"][0].Handler = HandleSetName;
            _config.Commands["/place-order"][0].Handler = HandleSetDirection;
            _config.Commands["/place-order"][1].Handler = HandleSetToken;
            _config.Commands["/place-order"][2].Handler = HandleSetTokenQuantity;
            _config.Commands["/place-order"][3].Handler = HandleSetTokenPrice;
            _config.Commands["/cancel-order"][0].Handler = HandleSelectOrderToCancel;
            _config.Commands["/cancel-order"][1].Handler = HandleConfirmCancellation;
            _config.Commands["/cancel-all-orders"][0].Handler = HandleConfirmCancellationAllOrders;
            _config.Commands["/get-profile"][0].Handler = HandleGetProfile;
            _config.Commands["/get-token-info"][0].Handler = HandleSelectTokenInfo;
            _config.Commands["/get-token-info"][1].Handler = HandleShowTokenInfo;
        }

        public async Task<(string? message, List<QuickButton>?)> ProcessInput(long userId, string input)
        {
            // Если это команда
            if (_config.Commands.ContainsKey(input))
            {
                return await StartCommand(userId, input);
            }

            // Если активная сессия
            if (_sessionStore.Sessions.ContainsKey(userId))
            {
                return await ContinueCommand(userId, input);
            }

            return ("Неизвестная команда", null);
        }

        private async Task<(string?, List<QuickButton>?)> StartCommand(long userId, string command)
        {
            if (command is "/cancel-order" or "/cancel-all-orders")
            {
                var hasActiveOrders = await _cancelOrderService.HasActiveOrdersAsync(userId);
                if (!hasActiveOrders)
                    return ("Нет активных ордеров для отмены.", null);
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
                _sessionStore.Sessions[userId] = session;

                var nextStep = commandSteps.First(s => s.Name == session.CurrentStep);

                var question = await _questionDecorator.DecorateQuestionAsync(nextStep.Name, nextStep.Question, session);
                var buttons = await _buttonDecorator.DecorateButtonsAsync(nextStep.Name, nextStep.Buttons, session);

                var step = _config.Commands[command].First();
                return (question, buttons);
            }
            else
            {
                var result = await currentStep.Handler(session, command);
                return (result.Message ?? "Готово!", null);
            }
        }

        private async Task<(string?, List<QuickButton>?)> ContinueCommand(long userId, string input)
        {
            var session = _sessionStore.Sessions[userId];
            var commandSteps = _config.Commands[session.CurrentCommand];
            var currentStep = commandSteps.First(s => s.Name == session.CurrentStep);

            // Выполняем handler
            var result = await currentStep.Handler(session, input);

            if (!result.Success)
            {
                // Ошибка - остаемся на текущем шаге
                return (result.Message, currentStep.Buttons);
            }

            // Успех - переходим к следующему шагу
            if (result.NextStep == "completed")
            {
                _sessionStore.Sessions.TryRemove(userId, out _);
                return (result.Message ?? "Готово!", null);
            }

            // Обновляем шаг и возвращаем следующий вопрос
            session.CurrentStep = result.NextStep;
            var nextStep = commandSteps.First(s => s.Name == result.NextStep);

            var question = await _questionDecorator.DecorateQuestionAsync(nextStep.Name, nextStep.Question, session);
            var buttons = await _buttonDecorator.DecorateButtonsAsync(nextStep.Name, nextStep.Buttons, session);

            return (question, buttons);
        }
    }
}
