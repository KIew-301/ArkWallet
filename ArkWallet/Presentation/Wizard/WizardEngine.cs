using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Decorators;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.SuggestionServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Entities.Configurations;

namespace ArkWallet.Infrastructure.Wizard
{
    internal partial class WizardEngine
    {
        private readonly WizardConfiguration _config;
        private readonly Dictionary<long, UserSession> _sessions = new();

        // TRADER SERVICES
        private readonly ITraderRegistrationService _traderRegistrationService;
        private readonly ITraderQueryService _traderQueryService;
        private readonly ITraderBalanceUpdatingService _traderBalanceUpdatingService;

        // ORDER SERVICES
        private readonly IOrderValidationService _orderValidationService;
        private readonly IOrderCreationService _orderCreationService;
        private readonly ICancelOrderService _cancelOrderService;
        private readonly IOrderQueryService _orderQueryService;

        // PORTFOLIO & TOKEN SERVICES
        private readonly IPortfolioQueryService _portfolioQueryService;
        private readonly IPortfolioUpdatingService _portfolioUpdatingService;
        private readonly ITokenQueryService _tokenQueryService;
        private readonly ITokenCreationService _tokenCreationServices;

        // SUGGESTION SERVICES
        private readonly IPriceSuggestionService _priceSuggestionService;
        private readonly IQuantitySuggestionService _quantitySuggestionService;

        // DECORATOR SERVICES
        private readonly IQuestionDecorator _questionDecorator;
        private readonly IButtonDecorator _buttonDecorator;

        public WizardEngine(
            ITraderRegistrationService traderRegistrationService,
            ITraderQueryService traderQueryService,
            ITraderBalanceUpdatingService traderBalanceUpdatingService,
            IOrderValidationService orderValidationService,
            IOrderCreationService orderCreationService,
            ICancelOrderService cancelOrderService,
            IOrderQueryService orderQueryService,
            IPortfolioQueryService portfolioQueryService,
            IPortfolioUpdatingService portfolioUpdatingService,
            ITokenQueryService tokenQueryService,
            ITokenCreationService tokenCreationServices,
            IPriceSuggestionService priceSuggestionService,
            IQuantitySuggestionService quantitySuggestionService,
            IQuestionDecorator questionDecorator,
            IButtonDecorator buttonDecorator
            )
        {
            _traderRegistrationService = traderRegistrationService;
            _traderQueryService = traderQueryService;
            _traderBalanceUpdatingService = traderBalanceUpdatingService;
            _orderValidationService = orderValidationService;
            _orderCreationService = orderCreationService;
            _cancelOrderService = cancelOrderService;
            _orderQueryService = orderQueryService;
            _portfolioQueryService = portfolioQueryService;
            _portfolioUpdatingService = portfolioUpdatingService;
            _tokenQueryService = tokenQueryService;
            _tokenCreationServices = tokenCreationServices;
            _priceSuggestionService = priceSuggestionService;
            _quantitySuggestionService = quantitySuggestionService;
            _questionDecorator = questionDecorator;
            _buttonDecorator = buttonDecorator;

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
        }

        public async Task<(string? message, List<QuickButton>? buttons)> ProcessInput(long userId, string input)
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

        private async Task<(string?, List<QuickButton>)> StartCommand(long userId, string command)
        {
            var session = new UserSession
            {
                Id = userId,
                CurrentCommand = command,
                CurrentStep = _config.Commands[command].First().Name
            };
            _sessions[userId] = session;

            var commandSteps = _config.Commands[session.CurrentCommand];

            var nextStep = commandSteps.First(s => s.Name == session.CurrentStep);

            var question = await _questionDecorator.DecorateQuestionAsync(nextStep.Name, nextStep.Question, session.Id, session.Data);
            var buttons = await _buttonDecorator.DecorateButtonsAsync(nextStep.Name, nextStep.Buttons, session.Id, session.Data);

            var step = _config.Commands[command].First();
            return (question, buttons);
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

            var question = await _questionDecorator.DecorateQuestionAsync(nextStep.Name, nextStep.Question, session.Id, session.Data);
            var buttons = await _buttonDecorator.DecorateButtonsAsync(nextStep.Name, nextStep.Buttons, session.Id, session.Data);

            return (question, buttons);
        }
    }
}
