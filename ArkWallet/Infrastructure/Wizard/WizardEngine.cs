using ArkWallet.Application.Services;
using ArkWallet.Data;
using ArkWallet.Infrastructure.Wizard;
using ArkWallet.Repositories;
using ArkWallet.ValueObjects;

namespace ArkWallet.Domain.Wizard
{
    internal partial class WizardEngine
    {
        private readonly WizardConfiguration _config;
        private readonly OrderService _orderService;
        private readonly QuestionDecorator _questionDecorator;
        private readonly Dictionary<long, UserSession> _sessions = new();

        private readonly TraderRepository _traderRepo;
        private readonly CharacterTokenRepository _tokenRepo;
        private readonly PortfolioItemRepository _portfolioRepo;

        public WizardEngine(WizardConfiguration config,
            TraderRepository traderRepo,
            CharacterTokenRepository tokenRepo,
            PortfolioItemRepository portfolioRepo,
            OrderService orderService,
            QuestionDecorator questionDecorator)
        {
            _config = config;

            _traderRepo = traderRepo;
            _tokenRepo = tokenRepo;
            _portfolioRepo = portfolioRepo;
            _orderService = orderService;

            ConfigureHandlers();
            ConfigureAdditionHandlers();
            _questionDecorator = questionDecorator;
        }

        private void ConfigureHandlers()
        {
            // Регистрация комманд
            _config.Commands["/start"][0].Handler = HandleSetName;
            _config.Commands["/placeorder"][0].Handler = HandleSetDirection;
            _config.Commands["/placeorder"][1].Handler = HandleSetToken;
            _config.Commands["/placeorder"][2].Handler = HandleSetTokenQuantity;
            _config.Commands["/placeorder"][3].Handler = HandleSetTokenPrice;
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

            var step = _config.Commands[command].First();
            return (step.Question, step.Buttons);
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
            session.Data.Add(currentStep.Name, input);
            session.CurrentStep = result.NextStep;
            var nextStep = commandSteps.First(s => s.Name == result.NextStep);

            string question = await _questionDecorator.Decorate(nextStep.Name, nextStep.Question, session);

            return (question, nextStep.Buttons);
        }
    }
}
