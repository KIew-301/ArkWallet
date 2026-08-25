using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Decorators;
using ArkWallet.Application.Contracts.Leaders;
using ArkWallet.Application.Contracts.MarketMaker;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Application.Contracts.Orchestrators;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Infrastructure.AccessControl;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Contracts.TradeServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Entities.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
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
        private readonly ITokenDeletionService _tokenDeletionService;

        // TRADE SERVICES
        private readonly ITradeQueryService _tradeQueryService;

        // LEADERS
        private readonly ILeadersTopByBalanceQueryService _leadersTopByBalanceQueryService;
        private readonly IBalanceSnapshotService _balanceSnapshotService;

        // ORCHESTRATORS
        private readonly ICandleOrchestrator _candleOrchestrator;

        // MARKET MAKER
        private readonly IMarketMakerBotQueryService _botQueryService;

        // AUTH
        private readonly ITokenService _tokenService;
        private readonly long _primaryAdminId;

        // STATS
        private readonly ITradingVolumeService _tradingVolumeService;

        // MINING SERVICES
        private readonly IMiningGlobalRuleQueryService _miningGlobalRuleQueryService;
        private readonly IMiningMachineQueryService _miningMachineQueryService;
        private readonly IMiningMachineSlotQueryService _miningMachineSlotQueryService;
        private readonly IMiningMachineSlotBuyingService _miningMachineSlotBuyingService;
        private readonly IMiningMachineCreationService _miningMachineCreationService;
        private readonly IMiningMachineRuleCreationService _miningMachineRuleCreationService;
        private readonly IMiningMachineDeletionService _miningMachineDeletionService;
        private readonly IMiningMachineRuleDeletionService _miningMachineRuleDeletionService;
        private readonly IMiningMachineUpdateService _miningMachineUpdateService;
        private readonly IMiningMachineRuleUpdateService _miningMachineRuleUpdateService;
        private readonly IMiningGlobalRuleUpdateService _miningGlobalRuleUpdateService;
        private readonly IAppStateQueryService _appStateQueryService;

        // MINING ORCHESTRATORS
        private readonly IMiningMachineCreationOrchestrator _miningMachineCreationOrchestrator;
        private readonly IMiningMachineSlotSwitchingOrchestrator _miningMachineSlotSwitchingOrchestrator;
        private readonly IMiningMachineSlotTakingTokenOrchestrator _miningMachineSlotTakingTokenOrchestrator;
        private readonly IMiningMachineSlotSellingOrchestrator _miningMachineSlotSellingOrchestrator;

        // BROADCAST
        private readonly IMessageSender _messageSender;

        // DECORATOR SERVICES
        private readonly IQuestionDecorator _questionDecorator;
        private readonly IButtonDecorator _buttonDecorator;

        // OBSERVABILITY
        private readonly IMetricsSnapshotService _metricsSnapshotService;

        // DB
        private readonly ArkWalletDbContext _dbContext;

        // ACCESS CONTROL
        private readonly AccessControlService _accessControl;

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
            ITokenDeletionService tokenDeletionService,
            ITradeQueryService tradeQueryService,
            ILeadersTopByBalanceQueryService leadersTopByBalanceQueryService,
            IBalanceSnapshotService balanceSnapshotService,
            ICandleOrchestrator candleOrchestrator,
            IMarketMakerBotQueryService botQueryService,
            ITokenService tokenService,
            ITradingVolumeService tradingVolumeService,
            IMessageSender messageSender,
            IConfiguration configuration,
            IQuestionDecorator questionDecorator,
            IButtonDecorator buttonDecorator,
            IMetricsSnapshotService metricsSnapshotService,
            IMiningGlobalRuleQueryService miningGlobalRuleQueryService,
            IMiningMachineQueryService miningMachineQueryService,
            IMiningMachineSlotQueryService miningMachineSlotQueryService,
            IMiningMachineSlotBuyingService miningMachineSlotBuyingService,
            IMiningMachineCreationService miningMachineCreationService,
            IMiningMachineRuleCreationService miningMachineRuleCreationService,
            IMiningMachineDeletionService miningMachineDeletionService,
            IMiningMachineRuleDeletionService miningMachineRuleDeletionService,
            IMiningMachineUpdateService miningMachineUpdateService,
            IMiningMachineRuleUpdateService miningMachineRuleUpdateService,
            IMiningGlobalRuleUpdateService miningGlobalRuleUpdateService,
            IAppStateQueryService appStateQueryService,
            IMiningMachineSlotSwitchingOrchestrator miningMachineSlotSwitchingOrchestrator,
            IMiningMachineCreationOrchestrator miningMachineCreationOrchestrator,
            IMiningMachineSlotTakingTokenOrchestrator miningMachineSlotTakingTokenOrchestrator,
            IMiningMachineSlotSellingOrchestrator miningMachineSlotSellingOrchestrator,
            WizardConfiguration config,
            ArkWalletDbContext dbContext,
            AccessControlService accessControl
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
            _tokenDeletionService = tokenDeletionService;
            _tradeQueryService = tradeQueryService;
            _leadersTopByBalanceQueryService = leadersTopByBalanceQueryService;
            _balanceSnapshotService = balanceSnapshotService;
            _candleOrchestrator = candleOrchestrator;
            _botQueryService = botQueryService;
            _tokenService = tokenService;
            _tradingVolumeService = tradingVolumeService;
            _messageSender = messageSender;
            _primaryAdminId = long.Parse(configuration["Telegram:AdminId:Main"] ?? "0");
            _questionDecorator = questionDecorator;
            _buttonDecorator = buttonDecorator;
            _metricsSnapshotService = metricsSnapshotService;
            _miningGlobalRuleQueryService = miningGlobalRuleQueryService;
            _miningMachineQueryService = miningMachineQueryService;
            _miningMachineSlotQueryService = miningMachineSlotQueryService;
            _miningMachineSlotBuyingService = miningMachineSlotBuyingService;
            _miningMachineCreationService = miningMachineCreationService;
            _miningMachineRuleCreationService = miningMachineRuleCreationService;
            _miningMachineDeletionService = miningMachineDeletionService;
            _miningMachineRuleDeletionService = miningMachineRuleDeletionService;
            _miningMachineUpdateService = miningMachineUpdateService;
            _miningMachineRuleUpdateService = miningMachineRuleUpdateService;
            _miningGlobalRuleUpdateService = miningGlobalRuleUpdateService;
            _appStateQueryService = appStateQueryService;
            _miningMachineSlotSwitchingOrchestrator = miningMachineSlotSwitchingOrchestrator;
            _miningMachineCreationOrchestrator = miningMachineCreationOrchestrator;
            _miningMachineSlotTakingTokenOrchestrator = miningMachineSlotTakingTokenOrchestrator;
            _miningMachineSlotSellingOrchestrator = miningMachineSlotSellingOrchestrator;
            _dbContext = dbContext;
            _accessControl = accessControl;
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
            _config.Commands["/mining_rules"][0].Handler = HandleGetMiningRules;
            _config.Commands["/mining_machines"][0].Handler = HandleGetMiningMachines;
            _config.Commands["/mining_slots"][0].Handler = HandleGetMiningSlots;
            _config.Commands["/mining_take_all"][0].Handler = HandleMiningTakeAll;
            _config.Commands["/mining_buy"][0].Handler = HandleMiningBuySelectMachine;
            _config.Commands["/mining_buy"][1].Handler = HandleMiningBuyConfirm;
            _config.Commands["/mining_switch"][0].Handler = HandleMiningSwitchSelectSlot;
            _config.Commands["/mining_switch"][1].Handler = HandleMiningSwitchSelectToken;
            _config.Commands["/mining_switch"][2].Handler = HandleMiningSwitchConfirm;
            _config.Commands["/mining_take"][0].Handler = HandleMiningTakeSelectSlot;
            _config.Commands["/mining_take"][1].Handler = HandleMiningTakeConfirm;
            _config.Commands["/mining_sell"][0].Handler = HandleMiningSellSelectSlot;
            _config.Commands["/mining_sell"][1].Handler = HandleMiningSellConfirm;
        }

        public async Task<WizardResult> ProcessInput(long userId, string input)
            => await ProcessInputInternal(userId, input, chatType: null);

        public async Task<WizardResult> ProcessInput(long userId, string input, ChatType? chatType)
            => await ProcessInputInternal(userId, input, chatType);

        private async Task<WizardResult> ProcessInputInternal(long userId, string input, ChatType? chatType)
        {
            var command = ResolveCommandName(input, userId);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Для групповых чатов: разрешаем только OneStep команды и quick paths (с аргументом)
                if (chatType.HasValue && chatType.Value != ChatType.Private)
                {
                    bool isCommandOneStep = _config.Commands.ContainsKey(command)
                        && _config.Commands[command].First().OneStep;

                    bool isQuickPath = input.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2
                        && command != "unknown";

                    if (!isCommandOneStep && !isQuickPath)
                        return new WizardResult { Message = "", ChatType = chatType.Value };
                }

                // Выполняем команду
                var result = await ExecuteCommandAsync(userId, input, command);
                
                // Применяем ChatType ко всем результатам
                if (chatType.HasValue)
                    result.ChatType = chatType.Value;
                
                // Фильтруем кнопки для групповых чатов
                if (chatType.HasValue && chatType.Value != ChatType.Private)
                {
                    result.Buttons = FilterButtonsForGroup(result.Buttons);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wizard ProcessInput failed for user {UserId}, input: {Input}", userId, input);
                return new WizardResult { Message = ServerErrorMessage, ChatType = chatType };
            }
            finally
            {
                stopwatch.Stop();
                ArkWalletMetrics.RecordCommand(command, stopwatch.Elapsed.TotalSeconds);
            }

            // Local function to avoid code duplication
            async Task<WizardResult> ExecuteCommandAsync(long uid, string inp, string cmd)
            {
                if (inp.StartsWith("/get_order_book "))
                {
                    var parts = inp.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 4)
                        return await HandleQuickOrderBook(parts[1], parts[2], parts[3]);
                }

                if (inp.StartsWith("/get_trades "))
                {
                    var parts = inp.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                        return await HandleQuickTrades(uid, parts[1]);
                }

                if (inp.StartsWith("/get_tops "))
                {
                    var parts = inp.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                        return await HandleQuickTops(uid, parts[1]);
                }

                if (inp.StartsWith("/admin_bots_activity "))
                {
                    var parts = inp.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                        return await HandleQuickAdminBotsActivity(parts[1]);
                }

                if (inp.StartsWith("/admin_stats "))
                {
                    var parts = inp.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                        return await HandleQuickAdminStats(parts[1]);
                }

                if (inp.StartsWith("/mining_buy "))
                {
                    var parts = inp.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                        return await HandleQuickMiningBuy(uid, parts[1]);
                }

                if (inp.StartsWith("/mining_take "))
                {
                    var parts = inp.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                        return await HandleQuickMiningTake(uid, parts[1]);
                }

                if (inp.StartsWith("/mining_sell "))
                {
                    var parts = inp.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                        return await HandleQuickMiningSell(uid, parts[1]);
                }

                if (_config.Commands.ContainsKey(inp))
                    return await StartCommand(uid, inp);

                if (_sessionStore.TryGet(uid, out var session) && session != null)
                    return await ContinueCommand(uid, inp, session);

                return new WizardResult { Message = "Неизвестная команда" };
            }
        }

        private string ResolveCommandName(string input, long userId)
        {
            if (input.StartsWith("/get_order_book ")
                || input.StartsWith("/get_trades ")
                || input.StartsWith("/get_tops ")
                || input.StartsWith("/admin_bots_activity ")
                || input.StartsWith("/admin_stats ")
                || input.StartsWith("/mining_buy ")
                || input.StartsWith("/mining_take ")
                || input.StartsWith("/mining_sell "))
            {
                return input.Split(' ', 2)[0];
            }

            if (_config.Commands.ContainsKey(input))
                return input;

            if (_sessionStore.TryGet(userId, out var session) && session != null)
                return session.CurrentCommand ?? "unknown";

            return "unknown";
        }

        private async Task<WizardResult> StartCommand(long userId, string command)
        {
            if (command is "/cancel_order" or "/cancel_all_orders")
            {
                var hasActiveOrders = await _cancelOrderService.HasActiveOrdersAsync(userId);
                if (!hasActiveOrders)
                    return new WizardResult { Message = "Нет активных ордеров для отмены." };
            }

            if (command == "/start")
            {
                var isRegistered = await _traderRegistrationService.CheckTraderAlreadyRegistered(userId);
                if (isRegistered)
                    return new WizardResult { Message = "Вы уже зарегистрированы! Используйте /get_profile для просмотра профиля." };
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

                return new WizardResult { Message = question, Buttons = buttons };
            }
            else
            {
                var result = await currentStep.Handler(session, command);

                if (!result.Success)
                {
                    _logger.LogWarning("Wizard OneStep handler error for user {UserId}, command {Command}: {Error}",
                        userId, command, result.Message);
                    return new WizardResult { Message = ErrorMessageFor(command, result.Message) };
                }

                return new WizardResult { Message = result.Message ?? "Готово!", Buttons = result.Buttons, SentFilePath = result.SentFilePath };
            }
        }

        private async Task<WizardResult> ContinueCommand(long userId, string input, UserSession session)
        {
            var commandSteps = _config.Commands[session.CurrentCommand];
            var currentStep = commandSteps.First(s => s.Name == session.CurrentStep);

            var result = await currentStep.Handler(session, input);

            if (!result.Success)
            {
                _logger.LogWarning("Wizard step error for user {UserId}, command {Command}, step {Step}: {Error}",
                    userId, session.CurrentCommand, session.CurrentStep, result.Message);
                return new WizardResult { Message = ErrorMessageFor(session.CurrentCommand, result.Message), Buttons = currentStep.Buttons };
            }

            if (result.NextStep == "completed")
            {
                _sessionStore.Remove(userId);
                return new WizardResult { Message = result.Message ?? "Готово!", Buttons = result.Buttons, SentFilePath = result.SentFilePath };
            }

            session.CurrentStep = result.NextStep;
            var nextStep = commandSteps.First(s => s.Name == result.NextStep);

            if (nextStep.OneStep)
            {
                _sessionStore.Remove(userId);
                if (nextStep.Handler == null)
                {
                    _logger.LogError("Handler not found for step {Step} in command {Command}", result.NextStep, session.CurrentCommand);
                    return new WizardResult { Message = ServerErrorMessage };
                }

                var oneStepResult = await nextStep.Handler(session, input);

                if (!oneStepResult.Success)
                {
                    _logger.LogWarning("Wizard OneStep handler error for user {UserId}, command {Command}, step {Step}: {Error}",
                        userId, session.CurrentCommand, result.NextStep, oneStepResult.Message);
                    return new WizardResult { Message = ErrorMessageFor(session.CurrentCommand, oneStepResult.Message) };
                }

                return new WizardResult { Message = oneStepResult.Message ?? "Готово!", Buttons = oneStepResult.Buttons, SentFilePath = oneStepResult.SentFilePath };
            }

            var question = await _questionDecorator.DecorateQuestionAsync(nextStep.Name, nextStep.Question, session);
            var buttons = await _buttonDecorator.DecorateButtonsAsync(nextStep.Name, nextStep.Buttons, session);

            return new WizardResult { Message = question, Buttons = buttons };
        }

        /// <summary>
        /// Для admin-команд показывает конкретное описание ошибки, для остальных — общее сообщение.
        /// </summary>
        private static string ErrorMessageFor(string command, string? error)
            => command.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)
                ? error ?? ServerErrorMessage
                : ServerErrorMessage;

        /// <summary>
        /// Убирает все кнопки для групповых чатов.
        /// </summary>
        private static List<QuickButton>? FilterButtonsForGroup(List<QuickButton>? buttons)
        {
            return null;
        }
    }
}
