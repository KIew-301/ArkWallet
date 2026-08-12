using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Application.Contracts.Orchestrators;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Infrastructure.Wizard
{
    partial class WizardEngine
    {
        private const string MiningMachineIdDataKey = "mining_machine_id";
        private const string MiningMachineNameDataKey = "mining_machine_name";
        private const string MiningSlotIdDataKey = "mining_slot_id";
        private const string MiningSlotNameDataKey = "mining_slot_name";

        public async Task<StepResult> HandleGetMiningRules(UserSession session, string input)
        {
            var rulesResult = await _miningGlobalRuleQueryService.TakeRulesAsync();

            if (!rulesResult.TryGetData(out var rules) || rules.Count == 0)
                return StepResult.Ok("completed", "Список правил майнинга пуст.");

            var lines = new List<string> { "⚙️ Глобальные правила майнинга:\n" };

            foreach (var rule in rules)
            {
                lines.Add($"{GetMiningStatusEmoji(rule.CurrentStatus)} {rule.TokenInfo.Symbol} ({rule.TokenInfo.Name})");
                lines.Add($"   Текущий статус: {GetMiningStatusText(rule.CurrentStatus)}");
                lines.Add($"   Будущий статус: {GetMiningStatusText(rule.FutureStatus)}");
                lines.Add($"   Базовая скорость: {rule.BaseMiningSpeed:F2} ток/мин");
                lines.Add($"   Базовая прибыль: {rule.BaseProfit:F2}{Descriptor.CurrencySymbol}");
                lines.Add("");
            }

            var buttons = new List<QuickButton>
            {
                new() { Text = RefreshButtonText, Value = "/mining_rules" }
            };

            var result = StepResult.Ok("completed", string.Join("\n", lines));
            result.Buttons = buttons;
            return result;
        }

        public async Task<StepResult> HandleGetMiningMachines(UserSession session, string input)
        {
            var machinesResult = await _miningMachineQueryService.TakeActiveForSaleMachinesAsync();

            if (!machinesResult.TryGetData(out var machines) || machines.Count == 0)
                return StepResult.Ok("completed", "Машины для покупки не найдены.");

            var lines = new List<string> { "🏭 Майнинг-машины в продаже:\n" };

            foreach (var machine in machines)
            {
                lines.Add($"🏭 {machine.Name} ({machine.Type})");
                lines.Add($"   Id: {machine.Id} | Цена: {machine.Cost:F2}{Descriptor.CurrencySymbol} | Возврат: {machine.Reusability}%");
                lines.Add($"   Переключение: {machine.SwitchingTime} мин | Макс. прибыль: {machine.MaxProfit:F2}{Descriptor.CurrencySymbol}");

                if (machine.TokensMiningData.Count > 0)
                {
                    var tokens = string.Join(", ", machine.TokensMiningData.Select(t => $"{t.Symbol} ({t.Profit:F2}{Descriptor.CurrencySymbol})"));
                    lines.Add($"   Токены: {tokens}");
                }

                lines.Add("");
            }

            lines.Add("Для покупки введите /mining_buy.");

            var buttons = new List<QuickButton>
            {
                new() { Text = RefreshButtonText, Value = "/mining_machines" }
            };

            var result = StepResult.Ok("completed", string.Join("\n", lines));
            result.Buttons = buttons;
            return result;
        }

        public async Task<StepResult> HandleGetMiningSlots(UserSession session, string input)
        {
            var slotsResult = await _miningMachineSlotQueryService.TakeSlotsByTraderAsync(session.Id);

            if (!slotsResult.TryGetData(out var slots) || slots.Count == 0)
                return StepResult.Ok("completed", "У вас пока нет майнинг-машин.");

            var lines = new List<string> { "🛠 Ваши майнинг-машины:\n" };

            foreach (var slot in slots)
            {
                var activeSymbol = string.IsNullOrEmpty(slot.ActiveTokenMiningData.Symbol)
                    ? "-"
                    : slot.ActiveTokenMiningData.Symbol;

                lines.Add($"{GetSlotStatusEmoji(slot.Status)} {slot.Name} ({slot.Type}) | Id: {slot.Id}");
                lines.Add($"   Статус: {slot.Status}");
                lines.Add($"   Накоплено: {slot.TokensAmountCollected:F2} ток.");
                lines.Add($"   Активный токен: {activeSymbol}");
                lines.Add($"   Переключение: {slot.SwitchingPercent:F0}%");
                lines.Add($"   Цена продажи: {slot.Cost:F2}{Descriptor.CurrencySymbol}");
                lines.Add("");
            }

            var buttons = new List<QuickButton>
            {
                new() { Text = RefreshButtonText, Value = "/mining_slots" }
            };

            var result = StepResult.Ok("completed", string.Join("\n", lines));
            result.Buttons = buttons;
            return result;
        }

        public async Task<StepResult> HandleMiningBuySelectMachine(UserSession session, string input)
        {
            if (!long.TryParse(input, out var machineId))
                return StepResult.Error("Введите корректный идентификатор машины.");

            var machinesResult = await _miningMachineQueryService.TakeActiveForSaleMachinesAsync();

            if (!machinesResult.TryGetData(out var machines))
                return StepResult.Error(machinesResult.Message ?? "Не удалось получить список машин.");

            var machine = machines.FirstOrDefault(m => m.Id == machineId);
            if (machine is null)
                return StepResult.Error("Машина не найдена в списке доступных для покупки.");

            session.Data[MiningMachineIdDataKey] = machineId;
            session.Data[MiningMachineNameDataKey] = machine.Name;

            return StepResult.Ok("confirm_buy",
                $"Выбранная машина: {machine.Name} ({machine.Type})\n" +
                $"Цена: {machine.Cost:F2}{Descriptor.CurrencySymbol}\n" +
                $"Возврат: {machine.Reusability}%");
        }

        public async Task<StepResult> HandleMiningBuyConfirm(UserSession session, string input)
        {
            if (input != "confirm")
                return StepResult.Ok("completed", "Покупка отменена.");

            var machineId = session.Data[MiningMachineIdDataKey] is long id
                ? id
                : throw new InvalidOperationException("Машина не выбрана.");

            var result = await _miningMachineSlotBuyingService.BuyMachineAsync(session.Id, machineId);

            if (!result.TryGetData(out var slotId))
                return StepResult.Error(result.Message);

            return StepResult.Ok("completed", $"🎉 Майнинг-машина куплена! Ваш слот: Id {slotId}.");
        }

        public async Task<StepResult> HandleMiningSwitchSelectSlot(UserSession session, string input)
        {
            var validationResult = await ValidateAndStoreMiningSlot(session, input);
            if (validationResult != null)
                return validationResult;

            return StepResult.Ok("select_token");
        }

        public async Task<StepResult> HandleMiningSwitchSelectToken(UserSession session, string input)
        {
            var tokenResult = await _tokenQueryService.GetTokenInfoAsync(input.ToUpper());

            if (!tokenResult.TryGetData(out _))
                return StepResult.Error("Токен не найден. Проверьте символ и попробуйте снова.");

            session.Data[TokenSymbolDataKey] = input.ToUpper();

            var slotName = session.Data[MiningSlotNameDataKey]?.ToString() ?? "Слот";
            return StepResult.Ok("confirm_switch", $"Переключить {slotName} на майнинг {input.ToUpper()}?");
        }

        public async Task<StepResult> HandleMiningSwitchConfirm(UserSession session, string input)
        {
            if (input != "confirm")
                return StepResult.Ok("completed", "Переключение отменено.");

            var slotId = session.Data[MiningSlotIdDataKey] is long id
                ? id
                : throw new InvalidOperationException("Слот не выбран.");

            var symbol = session.Data[TokenSymbolDataKey]?.ToString();
            if (string.IsNullOrEmpty(symbol))
                return StepResult.Error("Токен не выбран.");

            var result = await _miningMachineSlotSwitchingOrchestrator.SwitchTargetTokenAsync(session.Id, slotId, symbol);

            return result.IsSuccess
                ? StepResult.Ok("completed", "🔄 Майнинг переключается на новый токен. Собранные токены возвращены в портфель.")
                : StepResult.Error(result.Message);
        }

        public async Task<StepResult> HandleMiningTakeSelectSlot(UserSession session, string input)
        {
            var validationResult = await ValidateAndStoreMiningSlot(session, input);
            if (validationResult != null)
                return validationResult;

            return StepResult.Ok("confirm_take");
        }

        public async Task<StepResult> HandleMiningTakeConfirm(UserSession session, string input)
        {
            if (input != "confirm")
                return StepResult.Ok("completed", "Снятие токенов отменено.");

            var slotId = session.Data[MiningSlotIdDataKey] is long id
                ? id
                : throw new InvalidOperationException("Слот не выбран.");

            var result = await _miningMachineSlotTakingTokenOrchestrator.TakeTokensFromMachineAsync(session.Id, slotId);

            return result.IsSuccess
                ? StepResult.Ok("completed", "🪙 Собранные токены сняты со слота и зачислены в портфель.")
                : StepResult.Error(result.Message);
        }

        public async Task<StepResult> HandleMiningSellSelectSlot(UserSession session, string input)
        {
            var validationResult = await ValidateAndStoreMiningSlot(session, input);
            if (validationResult != null)
                return validationResult;

            var slotName = session.Data[MiningSlotNameDataKey]?.ToString() ?? "Слот";
            return StepResult.Ok("confirm_sell", $"Продать {slotName}?");
        }

        public async Task<StepResult> HandleMiningSellConfirm(UserSession session, string input)
        {
            if (input != "confirm")
                return StepResult.Ok("completed", "Продажа отменена.");

            var slotId = session.Data[MiningSlotIdDataKey] is long id
                ? id
                : throw new InvalidOperationException("Слот не выбран.");

            var result = await _miningMachineSlotSellingOrchestrator.SellMachineAsync(session.Id, slotId);

            return result.IsSuccess
                ? StepResult.Ok("completed", "💰 Слот продан. Выручка зачислена на баланс, токены возвращены в портфель.")
                : StepResult.Error(result.Message);
        }

        private async Task<StepResult?> ValidateAndStoreMiningSlot(UserSession session, string input)
        {
            if (!long.TryParse(input, out var slotId))
                return StepResult.Error("Введите корректный идентификатор слота.");

            var slotsResult = await _miningMachineSlotQueryService.TakeSlotsByTraderAsync(session.Id);

            if (!slotsResult.TryGetData(out var slots))
                return StepResult.Error(slotsResult.Message ?? "Не удалось получить список слотов.");

            var slot = slots.FirstOrDefault(s => s.Id == slotId);
            if (slot is null)
                return StepResult.Error("Слот не найден. Проверьте идентификатор и попробуйте снова.");

            session.Data[MiningSlotIdDataKey] = slotId;
            session.Data[MiningSlotNameDataKey] = slot.Name;
            return null;
        }

        private static string GetMiningStatusEmoji(MiningStatus status) => status switch
        {
            MiningStatus.Profitable => "🟢",
            MiningStatus.Stable => "🟡",
            _ => "🔴"
        };

        private static string GetMiningStatusText(MiningStatus status) => status switch
        {
            MiningStatus.Profitable => "Прибыльный",
            MiningStatus.Stable => "Стабильный",
            _ => "Убыточный"
        };

        private static string GetSlotStatusEmoji(string status) => status switch
        {
            "Active" => "🟢",
            "Switching" => "🔄",
            "Passive" => "⚪",
            _ => "⚫"
        };
    }
}
