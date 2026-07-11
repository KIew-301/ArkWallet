using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Decorators;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Entities.Configurations;
using ArkWallet.Infrastructure.Data;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Infrastructure.Wizard
{
    [ExcludeFromCodeCoverage(Justification = "UI-оркестратор Telegram-бота, управляет сессиями пользователей. Зависит от внешнего Telegram API, тестируется интеграционно.")]
    internal partial class WizardEngine
    {
        private readonly WizardConfiguration _config;
        private readonly Dictionary<long, UserSession> _sessions = new();

        // TRADER SERVICES
        private readonly ITraderRegistrationService _traderRegistrationService;
        private readonly ITraderBalanceUpdatingService _traderBalanceUpdatingService;

        // ORDER SERVICES
        private readonly IOrderValidationService _orderValidationService;
        private readonly IOrderCreationService _orderCreationService;
        private readonly IOrderCancellationService _cancelOrderService;

        // PORTFOLIO & TOKEN SERVICES
        private readonly IPortfolioQueryService _portfolioQueryService;
        private readonly IPortfolioUpdatingService _portfolioUpdatingService;
        private readonly ITokenCreationService _tokenCreationServices;

        // DECORATOR SERVICES
        private readonly IQuestionDecorator _questionDecorator;
        private readonly IButtonDecorator _buttonDecorator;

        // DB CONTEXT
        private readonly ArkWalletDbContext _dbContext;

        public WizardEngine(
            ArkWalletDbContext dbContext,
            ITraderRegistrationService traderRegistrationService,
            ITraderBalanceUpdatingService traderBalanceUpdatingService,
            IOrderValidationService orderValidationService,
            IOrderCreationService orderCreationService,
            IOrderCancellationService cancelOrderService,
            IPortfolioQueryService portfolioQueryService,
            IPortfolioUpdatingService portfolioUpdatingService,
            ITokenCreationService tokenCreationServices,
            IQuestionDecorator questionDecorator,
            IButtonDecorator buttonDecorator,
            WizardConfiguration config
            )
        {
            _dbContext = dbContext;
            _traderRegistrationService = traderRegistrationService;
            _traderBalanceUpdatingService = traderBalanceUpdatingService;
            _orderValidationService = orderValidationService;
            _orderCreationService = orderCreationService;
            _cancelOrderService = cancelOrderService;
            _portfolioQueryService = portfolioQueryService;
            _portfolioUpdatingService = portfolioUpdatingService;
            _tokenCreationServices = tokenCreationServices;
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
            _config.Commands["/placeorder"][0].Handler = HandleSetDirection;
            _config.Commands["/placeorder"][1].Handler = HandleSetToken;
            _config.Commands["/placeorder"][2].Handler = HandleSetTokenQuantity;
            _config.Commands["/placeorder"][3].Handler = HandleSetTokenPrice;
            _config.Commands["/cancelorder"][0].Handler = HandleSelectOrderToCancel;
            _config.Commands["/cancelorder"][1].Handler = HandleConfirmCancellation;
            _config.Commands["/cancelallorders"][0].Handler = HandleConfirmCancellationAllOrders;
            _config.Commands["/getprofile"][0].Handler = HandleGetProfile;
        }

        public async Task<(string? message, List<QuickButton>?)> ProcessInput(long userId, string input)
        {
            // Если это команда
            if (_config.Commands.ContainsKey(input))
            {
                return await StartCommand(userId, input);
            }

            // Если активная сессия
            if (_sessions.ContainsKey(userId))
            {
                return await ContinueCommand(userId, input);
            }

            return ("Неизвестная команда", null);
        }

        private async Task<(string?, List<QuickButton>?)> StartCommand(long userId, string command)
        {
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
                _sessions[userId] = session;

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
            var session = _sessions[userId];
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
                _sessions.Remove(userId);
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
