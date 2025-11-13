using ArkWallet.Application.Services;
using ArkWallet.Entities.Configurations;
using ArkWallet.Application.Contracts;
using ArkWallet.Infrastructure.Services.Wizard;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Infrastructure.Wizard
{
    internal partial class WizardEngine
    {
        private readonly WizardConfiguration _config;
        private readonly OrderService _orderService;
        private readonly QuestionDecorator _questionDecorator;
        private readonly KeywordDecorator _keywordDecorator;
        private readonly Dictionary<long, UserSession> _sessions = new();

        private readonly IUnitOfWork _uow;

        public WizardEngine(WizardConfiguration config,
            OrderService orderService,
            QuestionDecorator questionDecorator,
            KeywordDecorator keywordDecorator,
            IUnitOfWork uow)
        {
            _config = config;
            _orderService = orderService;
            _questionDecorator = questionDecorator;
            _keywordDecorator = keywordDecorator;
            _uow = uow; 
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
            session.CurrentStep = result.NextStep;
            var nextStep = commandSteps.First(s => s.Name == result.NextStep);

            var question = await _questionDecorator.Decorate(nextStep.Name, nextStep.Question, session);
            var buttons = await _keywordDecorator.Decorate(nextStep.Name, nextStep.Buttons, session);

            return (question, buttons);
        }
    }
}
