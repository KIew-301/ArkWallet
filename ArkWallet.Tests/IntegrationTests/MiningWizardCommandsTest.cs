using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Domain.Engines;
using ArkWallet.Infrastructure.Wizard;
using Moq;

namespace ArkWallet.Tests.IntegrationTests;

public class MiningWizardCommandsTest
{
    private readonly ServiceMocks _m;
    private readonly WizardEngine _engine;

    private const long UserId = 1001;

    public MiningWizardCommandsTest()
    {
        _m = WizardEngineTestHelper.Build();
        _engine = _m.Engine;
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n");

    private static TokensMiningData TokenData(string symbol, decimal profit)
        => new(string.Empty, symbol, 0m, profit);

    private void SetupMachines(List<MiningMachineData>? machines = null)
    {
        _m.MiningMachineQuery
            .Setup(s => s.TakeActiveForSaleMachinesAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<List<MiningMachineData>>.Ok(machines ?? new List<MiningMachineData>
            {
                new(1, "SM-01", "SMAI", 150m, 10, 50, 10000m,
                    new List<TokensMiningData> { TokenData("ARK_001", 250m) },
                    new List<TokensMiningData>())
            }));
    }

    private void SetupSlots(List<MiningMachineSlotData>? slots = null)
    {
        _m.MiningMachineSlotQuery
            .Setup(s => s.TakeSlotsByTraderAsync(UserId))
            .ReturnsAsync(Result<List<MiningMachineSlotData>>.Ok(slots ?? new List<MiningMachineSlotData>
            {
                new(5, "SM-01", "SMAI", "Active", 12.5m, 0m, 10, 5000m,
                    new ActiveTokenMiningData(string.Empty, "ARK_001", 5m, 250m),
                    new List<TokensMiningData>(),
                    new List<TokensMiningData>())
            }));
    }

    // ═══════════════════════════════════════════════════════════
    //  /mining_rules
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task MiningRules_NoRules_ReturnsEmptyMessage()
    {
        _m.MiningGlobalRuleQuery
            .Setup(s => s.TakeRulesAsync())
            .ReturnsAsync(Result<List<TokensMiningRuleData>>.Ok(new List<TokensMiningRuleData>()));

        var result = await _engine.ProcessInput(UserId, "/mining_rules");

        Assert.Equal("Список правил майнинга пуст.", result.Message);
    }

    [Fact]
    public async Task MiningRules_WithRules_ShowsRulesAndRefreshButton()
    {
        _m.MiningGlobalRuleQuery
            .Setup(s => s.TakeRulesAsync())
            .ReturnsAsync(Result<List<TokensMiningRuleData>>.Ok(new List<TokensMiningRuleData>
            {
                new(new TokenInfoDto("ARK_001", "Ark Knight", 100m),
                    "Profitable", "Stable", 50m, 5000m)
            }));

        var result = await _engine.ProcessInput(UserId, "/mining_rules");

        Assert.NotNull(result.Message);
        var msg = Normalize(result.Message);
        Assert.Contains("⚙️ Глобальные правила майнинга", msg);
        Assert.Contains("ARK_001", msg);
        Assert.Contains("Прибыльный", msg);
        Assert.Contains("Стабильный", msg);
        Assert.Contains("50,00", msg);
        Assert.Contains("🔄 Обновить", result.Buttons?.First().Text ?? string.Empty);
    }

    // ═══════════════════════════════════════════════════════════
    //  /mining_machines
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task MiningMachines_Empty_ReturnsEmptyMessage()
    {
        SetupMachines(new List<MiningMachineData>());

        var result = await _engine.ProcessInput(UserId, "/mining_machines");

        Assert.Equal("Машины для покупки не найдены.", result.Message);
    }

    [Fact]
    public async Task MiningMachines_WithMachines_ShowsMachineAndRefreshButton()
    {
        SetupMachines();

        var result = await _engine.ProcessInput(UserId, "/mining_machines");

        Assert.NotNull(result.Message);
        var msg = Normalize(result.Message);
        Assert.Contains("🏭 Майнинг-машины в продаже", msg);
        Assert.Contains("SM-01", msg);
        Assert.Contains("ARK_001", msg);
        Assert.Contains("/mining_buy", msg);
        Assert.Contains("🔄 Обновить", result.Buttons?.First().Text ?? string.Empty);
    }

    // ═══════════════════════════════════════════════════════════
    //  /mining_slots
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task MiningSlots_Empty_ReturnsEmptyMessage()
    {
        SetupSlots(new List<MiningMachineSlotData>());

        var result = await _engine.ProcessInput(UserId, "/mining_slots");

        Assert.Equal("У вас пока нет майнинг-машин.", result.Message);
    }

    [Fact]
    public async Task MiningSlots_WithSlots_ShowsSlotAndRefreshButton()
    {
        SetupSlots();

        var result = await _engine.ProcessInput(UserId, "/mining_slots");

        Assert.NotNull(result.Message);
        var msg = Normalize(result.Message);
        Assert.Contains("🛠 Ваши майнинг-машины", msg);
        Assert.Contains("SM-01", msg);
        Assert.Contains("ARK_001", msg);
        Assert.Contains("12,50", msg);
        Assert.Contains("🔄 Обновить", result.Buttons?.First().Text ?? string.Empty);
    }

    // ═══════════════════════════════════════════════════════════
    //  /mining_buy
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task MiningBuy_InvalidMachineId_ReturnsServerError()
    {
        SetupMachines();

        await _engine.ProcessInput(UserId, "/mining_buy");
        var result = await _engine.ProcessInput(UserId, "abc");

        Assert.Equal("Ошибка на стороне сервера", result.Message);
    }

    [Fact]
    public async Task MiningBuy_MachineNotInSale_ReturnsServerError()
    {
        SetupMachines();

        await _engine.ProcessInput(UserId, "/mining_buy");
        var result = await _engine.ProcessInput(UserId, "999");

        Assert.Equal("Ошибка на стороне сервера", result.Message);
    }

    [Fact]
    public async Task MiningBuy_ValidMachine_ShowsConfirmation()
    {
        SetupMachines();

        var start = await _engine.ProcessInput(UserId, "/mining_buy");
        Assert.Equal("Введите идентификатор машины для покупки:", start.Message);

        var confirm = await _engine.ProcessInput(UserId, "1");

        Assert.Equal("Подтвердите покупку машины:", confirm.Message);
        Assert.NotNull(confirm.Buttons);
        Assert.Contains(confirm.Buttons, b => b.Value == "confirm");
        Assert.Contains(confirm.Buttons, b => b.Value == "cancel");
    }

    [Fact]
    public async Task MiningBuy_Cancel_ReturnsCancelled()
    {
        SetupMachines();
        await _engine.ProcessInput(UserId, "/mining_buy");
        await _engine.ProcessInput(UserId, "1");

        var result = await _engine.ProcessInput(UserId, "cancel");

        Assert.Equal("Покупка отменена.", result.Message);
    }

    [Fact]
    public async Task MiningBuy_Confirm_BuysMachine()
    {
        SetupMachines();
        _m.MiningMachineSlotBuying
            .Setup(s => s.BuyMachineAsync(UserId, 1))
            .ReturnsAsync(Result<long>.Ok(42));

        await _engine.ProcessInput(UserId, "/mining_buy");
        await _engine.ProcessInput(UserId, "1");

        var result = await _engine.ProcessInput(UserId, "confirm");

        Assert.Equal("🎉 Майнинг-машина куплена! Ваш слот: Id 42.", result.Message);
    }

    [Fact]
    public async Task MiningBuy_ConfirmServiceFail_ReturnsServerError()
    {
        SetupMachines();
        _m.MiningMachineSlotBuying
            .Setup(s => s.BuyMachineAsync(UserId, 1))
            .ReturnsAsync(Result<long>.Fail("Недостаточно средств"));

        await _engine.ProcessInput(UserId, "/mining_buy");
        await _engine.ProcessInput(UserId, "1");

        var result = await _engine.ProcessInput(UserId, "confirm");

        Assert.Equal("Ошибка на стороне сервера", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  /mining_switch
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task MiningSwitch_FullFlow_SwitchesToken()
    {
        SetupSlots();
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("ZZZ"))
            .ReturnsAsync(Result<TokenInfo>.Ok(new TokenInfo("ZZZ", "Test", 100m, "", "")));
        _m.MiningMachineSlotSwitchingOrchestrator
            .Setup(s => s.SwitchTargetTokenAsync(UserId, 5, "ZZZ"))
            .ReturnsAsync(Result.Ok());

        var start = await _engine.ProcessInput(UserId, "/mining_switch");
        Assert.Equal("Введите идентификатор слота для переключения:", start.Message);

        var step1 = await _engine.ProcessInput(UserId, "5");
        Assert.Equal("На какой токен переключить майнинг? (напишите символ)", step1.Message);

        var step2 = await _engine.ProcessInput(UserId, "ZZZ");
        Assert.Equal("Подтвердите переключение майнинга:", step2.Message);

        var result = await _engine.ProcessInput(UserId, "confirm");

        Assert.NotNull(result.Message);
        Assert.Contains("🔄 Майнинг переключается", result.Message);
    }

    [Fact]
    public async Task MiningSwitch_InvalidSlot_ReturnsServerError()
    {
        SetupSlots();

        await _engine.ProcessInput(UserId, "/mining_switch");
        var result = await _engine.ProcessInput(UserId, "777");

        Assert.Equal("Ошибка на стороне сервера", result.Message);
    }

    [Fact]
    public async Task MiningSwitch_InvalidToken_ReturnsServerError()
    {
        SetupSlots();
        _m.TokenQuery
            .Setup(s => s.GetTokenInfoAsync("NONEXISTENT"))
            .ReturnsAsync(Result<TokenInfo>.Fail("Токен не найден"));

        await _engine.ProcessInput(UserId, "/mining_switch");
        await _engine.ProcessInput(UserId, "5");

        var result = await _engine.ProcessInput(UserId, "NONEXISTENT");

        Assert.Equal("Ошибка на стороне сервера", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  /mining_take
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task MiningTake_FullFlow_TakesTokens()
    {
        SetupSlots();
        _m.MiningMachineSlotTakingTokenOrchestrator
            .Setup(s => s.TakeTokensFromMachineAsync(UserId, 5))
            .ReturnsAsync(Result.Ok());

        var start = await _engine.ProcessInput(UserId, "/mining_take");
        Assert.Equal("Введите идентификатор слота для снятия токенов:", start.Message);

        var step1 = await _engine.ProcessInput(UserId, "5");
        Assert.Equal("Подтвердите снятие собранных токенов:", step1.Message);

        var result = await _engine.ProcessInput(UserId, "confirm");

        Assert.NotNull(result.Message);
        Assert.Contains("🪙", result.Message);
    }

    [Fact]
    public async Task MiningTake_Cancel_ReturnsCancelled()
    {
        SetupSlots();
        await _engine.ProcessInput(UserId, "/mining_take");
        await _engine.ProcessInput(UserId, "5");

        var result = await _engine.ProcessInput(UserId, "cancel");

        Assert.Equal("Снятие токенов отменено.", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  /mining_sell
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task MiningSell_FullFlow_SellsSlot()
    {
        SetupSlots();
        _m.MiningMachineSlotSellingOrchestrator
            .Setup(s => s.SellMachineAsync(UserId, 5))
            .ReturnsAsync(Result.Ok());

        var start = await _engine.ProcessInput(UserId, "/mining_sell");
        Assert.Equal("Введите идентификатор слота для продажи:", start.Message);

        var step1 = await _engine.ProcessInput(UserId, "5");
        Assert.Equal("Подтвердите продажу слота:", step1.Message);

        var result = await _engine.ProcessInput(UserId, "confirm");

        Assert.NotNull(result.Message);
        Assert.Contains("💰", result.Message);
    }

    [Fact]
    public async Task MiningSell_Cancel_ReturnsCancelled()
    {
        SetupSlots();
        await _engine.ProcessInput(UserId, "/mining_sell");
        await _engine.ProcessInput(UserId, "5");

        var result = await _engine.ProcessInput(UserId, "cancel");

        Assert.Equal("Продажа отменена.", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  Admin: /admin_help_mining and /admin_help
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminHelpMining_ShowsMiningCommands()
    {
        var result = await _engine.ProcessInput(UserId, "/admin_help_mining");

        Assert.NotNull(result.Message);
        var msg = Normalize(result.Message);
        Assert.Contains("Mining commands", msg);
        Assert.Contains("/admin_mining_create_machine", msg);
        Assert.Contains("/admin_mining_update_global_rule", msg);
    }

    [Fact]
    public async Task AdminHelp_IncludesMiningCategory()
    {
        var result = await _engine.ProcessInput(UserId, "/admin_help");

        Assert.NotNull(result.Message);
        Assert.Contains("/admin_help_mining — Mining commands", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  Admin: create machine / create rule
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminMiningCreateMachine_ValidJson_CreatesMachine()
    {
        _m.MiningMachineCreation
            .Setup(s => s.CreateMachineAsync(It.IsAny<MiningMachineCreationCommand>()))
            .ReturnsAsync(Result<MiningMachineCreationData>.Ok(new MiningMachineCreationData(1, "SM-01")));

        var start = await _engine.ProcessInput(UserId, "/admin_mining_create_machine");
        Assert.Contains("Send JSON to create a mining machine", start.Message);

        var result = await _engine.ProcessInput(UserId,
            "{\"name\":\"SM-01\",\"type\":\"SMAI\",\"switchingTime\":10,\"reusability\":50,\"isActiveForSale\":true,\"cost\":10000,\"image\":\"img\"}");

        Assert.Equal("Machine 'SM-01' created (Id: 1).", result.Message);
    }

    [Fact]
    public async Task AdminMiningCreateMachine_InvalidJson_ReturnsServerError()
    {
        var start = await _engine.ProcessInput(UserId, "/admin_mining_create_machine");

        var result = await _engine.ProcessInput(UserId, "not json");

        Assert.Equal("Ошибка на стороне сервера", result.Message);
        Assert.NotNull(start.Message);
    }

    [Fact]
    public async Task AdminMiningCreateRule_ValidJson_CreatesRule()
    {
        _m.MiningMachineRuleCreation
            .Setup(s => s.CreateRuleAsync(It.IsAny<MiningMachineRuleCreationCommand>()))
            .ReturnsAsync(Result<long>.Ok(7));

        await _engine.ProcessInput(UserId, "/admin_mining_create_rule");

        var result = await _engine.ProcessInput(UserId,
            "{\"miningMachineId\":1,\"characterTokenId\":\"ARK_001\",\"miningCoefficient\":1.5}");

        Assert.Equal("Rule created (Id: 7).", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  Admin: delete / deactivate machine, delete rule
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminMiningDeleteMachine_Confirm_DeletesMachine()
    {
        _m.MiningMachineDeletion
            .Setup(s => s.DeleteMachineAsync(5))
            .ReturnsAsync(Result.Ok());

        var start = await _engine.ProcessInput(UserId, "/admin_mining_delete_machine");
        Assert.Contains("Enter mining machine Id to delete", start.Message);

        var step1 = await _engine.ProcessInput(UserId, "5");
        Assert.Contains("PERMANENTLY delete", step1.Message);

        var result = await _engine.ProcessInput(UserId, "confirm");

        Assert.Equal("Machine 5 deleted.", result.Message);
    }

    [Fact]
    public async Task AdminMiningDeleteMachine_Cancel_ReturnsCancelled()
    {
        await _engine.ProcessInput(UserId, "/admin_mining_delete_machine");
        await _engine.ProcessInput(UserId, "5");

        var result = await _engine.ProcessInput(UserId, "cancel");

        Assert.Equal("Deletion cancelled.", result.Message);
    }

    [Fact]
    public async Task AdminMiningDeactivateMachine_Confirm_DeactivatesMachine()
    {
        _m.MiningMachineDeletion
            .Setup(s => s.DeactivateMachineAsync(5))
            .ReturnsAsync(Result.Ok());

        var start = await _engine.ProcessInput(UserId, "/admin_mining_deactivate_machine");
        Assert.Contains("Enter mining machine Id to deactivate", start.Message);

        await _engine.ProcessInput(UserId, "5");

        var result = await _engine.ProcessInput(UserId, "confirm");

        Assert.Equal("Machine 5 deactivated.", result.Message);
    }

    [Fact]
    public async Task AdminMiningDeleteRule_Confirm_DeletesRule()
    {
        _m.MiningMachineRuleDeletion
            .Setup(s => s.DeleteRuleAsync(7))
            .ReturnsAsync(Result.Ok());

        var start = await _engine.ProcessInput(UserId, "/admin_mining_delete_rule");
        Assert.Contains("Enter mining rule Id to delete", start.Message);

        await _engine.ProcessInput(UserId, "7");

        var result = await _engine.ProcessInput(UserId, "confirm");

        Assert.Equal("Rule 7 deleted.", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  Admin: update global rule, app state
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminMiningUpdateGlobalRule_ValidJson_UpdatesRule()
    {
        _m.MiningGlobalRuleUpdate
            .Setup(s => s.UpdateRuleAsync(It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<decimal?>(), It.IsAny<decimal?>()))
            .ReturnsAsync(Result.Ok());

        await _engine.ProcessInput(UserId, "/admin_mining_update_global_rule");

        var result = await _engine.ProcessInput(UserId,
            "{\"symbol\":\"ARK_001\",\"currentCoefficient\":1.05,\"futureCoefficient\":0.95,\"baseTokenMiningSpeed\":50}");

        Assert.Equal("Global rule for ARK_001 updated.", result.Message);
    }

    [Fact]
    public async Task AdminMiningUpdateGlobalRule_InvalidJson_ReturnsServerError()
    {
        await _engine.ProcessInput(UserId, "/admin_mining_update_global_rule");

        var result = await _engine.ProcessInput(UserId, "{not valid json");

        Assert.Equal("Ошибка на стороне сервера", result.Message);
    }

    [Fact]
    public async Task AdminMiningAppState_WithRecords_ShowsAllRecords()
    {
        _m.AppStateQuery
            .Setup(s => s.TakeAllAsync())
            .ReturnsAsync(Result<List<AppStateData>>.Ok(new List<AppStateData>
            {
                new("LastCalculation", "{\"date\":\"2026-01-01\"}"),
                new("LastUpdate", "{\"date\":\"2026-01-02\"}")
            }));

        var result = await _engine.ProcessInput(UserId, "/admin_mining_app_state");

        Assert.NotNull(result.Message);
        var msg = Normalize(result.Message);
        Assert.Contains("=== App State ===", msg);
        Assert.Contains("LastCalculation", msg);
        Assert.Contains("LastUpdate", msg);
    }

    [Fact]
    public async Task AdminMiningAppState_Empty_ReturnsEmptyMessage()
    {
        _m.AppStateQuery
            .Setup(s => s.TakeAllAsync())
            .ReturnsAsync(Result<List<AppStateData>>.Ok(new List<AppStateData>()));

        var result = await _engine.ProcessInput(UserId, "/admin_mining_app_state");

        Assert.Equal("No app state records.", result.Message);
    }
}
