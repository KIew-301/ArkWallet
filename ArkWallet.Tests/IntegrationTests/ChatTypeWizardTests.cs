using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Wizard;
using Moq;

namespace ArkWallet.Tests.IntegrationTests;

public class ChatTypeWizardTests
{
    private readonly ServiceMocks _m;
    private readonly WizardEngine _engine;

    private const long UserId = 2001;

    public ChatTypeWizardTests()
    {
        _m = WizardEngineTestHelper.Build();
        _engine = _m.Engine;
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n");

    // ═══════════════════════════════════════════════════════════
    //  ChatType filtering
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ProcessInput_PrivateChat_AllButtonsReturned()
    {
        _m.MiningGlobalRuleQuery
            .Setup(s => s.TakeRulesAsync())
            .ReturnsAsync(Result<List<TokensMiningRuleData>>.Ok(new List<TokensMiningRuleData>
            {
                new(new TokenInfoDto("ARK_001", "Ark Knight", 100m),
                    "Profitable", "Stable", 50m, 5000m)
            }));

        var result = await _engine.ProcessInput(UserId, "/mining_rules", ChatType.Private);

        Assert.NotNull(result.Message);
        Assert.Equal(ChatType.Private, result.ChatType);
        Assert.NotNull(result.Buttons);
        Assert.Single(result.Buttons);
        Assert.Equal("🔄 Обновить", result.Buttons[0].Text);
    }

    [Fact]
    public async Task ProcessInput_GroupChat_NoButtons()
    {
        _m.MiningGlobalRuleQuery
            .Setup(s => s.TakeRulesAsync())
            .ReturnsAsync(Result<List<TokensMiningRuleData>>.Ok(new List<TokensMiningRuleData>
            {
                new(new TokenInfoDto("ARK_001", "Ark Knight", 100m),
                    "Profitable", "Stable", 50m, 5000m)
            }));

        // В групповом чате команды работают, но кнопки убираются
        var result = await _engine.ProcessInput(UserId, "/mining_rules", ChatType.Group);

        Assert.NotNull(result.Message);
        Assert.Contains("⚙️ Глобальные правила майнинга", result.Message);
        Assert.Equal(ChatType.Group, result.ChatType);
        Assert.Null(result.Buttons);
    }

    [Fact]
    public async Task ProcessInput_GroupChat_OneStepCommand_WorksWithNoButtons()
    {
        _m.MiningGlobalRuleQuery
            .Setup(s => s.TakeRulesAsync())
            .ReturnsAsync(Result<List<TokensMiningRuleData>>.Ok(new List<TokensMiningRuleData>
            {
                new(new TokenInfoDto("ARK_001", "Ark Knight", 100m),
                    "Profitable", "Stable", 50m, 5000m)
            }));

        // /mining_slots - OneStep команда, должна работать в группах без кнопок
        var result = await _engine.ProcessInput(UserId, "/mining_slots", ChatType.Group);

        Assert.NotNull(result.Message);
        Assert.Equal(ChatType.Group, result.ChatType);
        Assert.Null(result.Buttons);
    }

    [Fact]
    public async Task ProcessInput_GroupChat_QuickPath_NoButtons()
    {
        _m.MiningMachineQuery
            .Setup(s => s.TakeActiveForSaleMachinesAsync(UserId))
            .ReturnsAsync(Result<List<MiningMachineData>>.Ok(new List<MiningMachineData>
            {
                new(1, "SM-01", "SMAI", 150m, 10, 50, 10000m,
                    new List<TokensMiningData>(),
                    new List<TokensMiningData>())
            }));

        // /mining_buy 1 - quick path, должен работать в группах без кнопок
        var result = await _engine.ProcessInput(UserId, "/mining_buy 1", ChatType.Group);

        Assert.NotNull(result.Message);
        Assert.Contains("Выбранная машина", result.Message);
        Assert.Equal(ChatType.Group, result.ChatType);
        Assert.Null(result.Buttons);
    }

    [Fact]
    public async Task ProcessInput_Supergroup_NoButtons()
    {
        _m.MiningGlobalRuleQuery
            .Setup(s => s.TakeRulesAsync())
            .ReturnsAsync(Result<List<TokensMiningRuleData>>.Ok(new List<TokensMiningRuleData>()));

        // Supergroup аналогично Group - кнопки убираются
        var result = await _engine.ProcessInput(UserId, "/mining_rules", ChatType.Supergroup);

        Assert.NotNull(result.Message);
        Assert.Equal(ChatType.Supergroup, result.ChatType);
        Assert.Null(result.Buttons);
    }

    [Fact]
    public async Task ProcessInput_NoChatType_AllButtons()
    {
        _m.MiningGlobalRuleQuery
            .Setup(s => s.TakeRulesAsync())
            .ReturnsAsync(Result<List<TokensMiningRuleData>>.Ok(new List<TokensMiningRuleData>
            {
                new(new TokenInfoDto("ARK_001", "Ark Knight", 100m),
                    "Profitable", "Stable", 50m, 5000m)
            }));

        // Без ChatType (null) - все команды доступны, кнопки не фильтруются
        var result = await _engine.ProcessInput(UserId, "/mining_rules");

        Assert.NotNull(result.Message);
        Assert.Null(result.ChatType);
        Assert.NotNull(result.Buttons);
        Assert.Single(result.Buttons);
    }

    // ═══════════════════════════════════════════════════════════
    //  Private chat: multi-step commands MUST work
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ProcessInput_PrivateChat_MultiStepCommand_ShowsFirstStep()
    {
        SetupMachines();

        // /mining_buy - это многошаговая команда (не OneStep)
        // В приватном чате она ДОЛЖНА работать
        var result = await _engine.ProcessInput(UserId, "/mining_buy", ChatType.Private);

        Assert.Equal("Введите идентификатор машины для покупки:", result.Message);
        Assert.Equal(ChatType.Private, result.ChatType);
        // Кнопки должны быть ВСЕ, не только "Обновить"
        Assert.NotNull(result.Buttons);
    }

    [Fact]
    public async Task ProcessInput_PrivateChat_MultiStepContinues()
    {
        SetupMachines();

        // Шаг 1: начинаем /mining_buy
        await _engine.ProcessInput(UserId, "/mining_buy", ChatType.Private);

        // Шаг 2: выбираем машину (многошаговый процесс)
        var result = await _engine.ProcessInput(UserId, "1", ChatType.Private);

        // Должна показать подтверждение покупки
        Assert.Equal("Подтвердите покупку машины:", result.Message);
        Assert.Equal(ChatType.Private, result.ChatType);
        Assert.NotNull(result.Buttons);
    }

    [Fact]
    public async Task ProcessInput_GroupChat_MultiStepCommand_Blocked()
    {
        SetupMachines();

        // /mining_buy - многошаговая команда
        // В групповом чате она НЕ ДОЛЖНА работать
        var result = await _engine.ProcessInput(UserId, "/mining_buy", ChatType.Group);

        // Должна вернуться пустая строка (команда заблокирована)
        Assert.Equal("", result.Message);
        Assert.Equal(ChatType.Group, result.ChatType);
        Assert.Null(result.Buttons);
    }

    [Fact]
    public async Task ProcessInput_GroupChat_OneStepCommand_NoButtons()
    {
        _m.MiningMachineSlotQuery
            .Setup(s => s.TakeSlotsByTraderAsync(UserId))
            .ReturnsAsync(Result<List<MiningMachineSlotData>>.Ok(new List<MiningMachineSlotData>()));

        // /mining_slots - OneStep команда
        // В групповом чате ДОЛЖНА работать без кнопок
        var result = await _engine.ProcessInput(UserId, "/mining_slots", ChatType.Group);

        Assert.Equal("У вас пока нет майнинг-машин.", result.Message);
        Assert.Equal(ChatType.Group, result.ChatType);
        Assert.Null(result.Buttons);
    }

    private void SetupMachines(List<MiningMachineData>? machines = null)
    {
        _m.MiningMachineQuery
            .Setup(s => s.TakeActiveForSaleMachinesAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<List<MiningMachineData>>.Ok(machines ?? new List<MiningMachineData>
            {
                new(1, "SM-01", "SMAI", 150m, 10, 50, 10000m,
                    new List<TokensMiningData>(),
                    new List<TokensMiningData>())
            }));
    }
}
