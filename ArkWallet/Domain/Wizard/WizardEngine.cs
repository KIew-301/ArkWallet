using ArkWallet.Data;
using ArkWallet.Repositories;
using ArkWallet.ValueObjects;

namespace ArkWallet.Domain.Wizard
{
    internal partial class WizardEngine
    {
        private readonly WizardConfiguration _config;
        private readonly Dictionary<long, UserSession> _sessions = new();

        private readonly TraderRepository _traderRepo;
        private readonly CharacterTokenRepository _tokenRepo;
        private readonly PortfolioItemRepository _portfolioRepo;

        public WizardEngine(WizardConfiguration config,
            TraderRepository traderRepo,
            CharacterTokenRepository tokenRepo,
            PortfolioItemRepository portfolioRepo)
        {
            _config = config;

            _traderRepo = traderRepo;
            _tokenRepo = tokenRepo;
            _portfolioRepo = portfolioRepo;

            ConfigureHandlers();
        }

        private void ConfigureHandlers()
        {
            // Регистрация
            _config.Commands["/start"][0].Handler = HandleSetName;
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

        private async Task<StepResult> HandleSetName(UserSession session, string input)
        {
            await AddNewTrader(session.Id, input);
            return StepResult.Ok("completed");
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
            return (nextStep.Question, nextStep.Buttons);
        }
    }
}
