using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.GlobalGoalContext.Application.Contracts.GlobalGoalServices;
using ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Infrastructure.Wizard;
using Moq;

namespace ArkWallet.Tests.IntegrationTests;

public class GlobalGoalWizardTests
{
    private readonly ServiceMocks _m;
    private readonly WizardEngine _engine;

    private const long UserId = 2001;

    public GlobalGoalWizardTests()
    {
        _m = WizardEngineTestHelper.Build();
        _engine = _m.Engine;
    }

    // ═══════════════════════════════════════════════════════════
    //  /global_goals
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GlobalGoals_NoGoals_ShowsEmptyMessage()
    {
        _m.GlobalGoalQuery
            .Setup(s => s.GetGoalsAsync())
            .ReturnsAsync(Result<List<GlobalGoalInfo>>.Ok(new List<GlobalGoalInfo>()));

        var result = await _engine.ProcessInput(UserId, "/global_goals");

        Assert.Equal("🎯 Глобальные цели пока не установлены.", result.Message);
    }

    [Fact]
    public async Task GlobalGoals_HasGoals_ShowsFormattedListWithProgressBar()
    {
        _m.GlobalGoalQuery
            .Setup(s => s.GetGoalsAsync())
            .ReturnsAsync(Result<List<GlobalGoalInfo>>.Ok(new List<GlobalGoalInfo>
            {
                new(
                    Id: 1,
                    Name: "Общий баланс",
                    Description: "Сумма балансов всех участников",
                    Target: 1000m,
                    Actual: 620m,
                    Progress: 0.62m,
                    AchievedCount: 2,
                    Steps: new List<GlobalGoalStepInfo>
                    {
                        new(3, 1500m, "ZZZ", 10m)
                    })
            }));

        var result = await _engine.ProcessInput(UserId, "/global_goals");

        Assert.NotNull(result.Message);
        Assert.Contains("🎯 Глобальные цели", result.Message);
        Assert.Contains("Общий баланс", result.Message);
        Assert.Contains("Сумма балансов всех участников", result.Message);
        Assert.Contains($"Цель: {1000m:N0}", result.Message);
        Assert.Contains("62%", result.Message);
        Assert.Contains("█", result.Message);
        Assert.Contains("░", result.Message);
        Assert.Contains("🏅 Достижений: 2", result.Message);
        Assert.Contains($"🎁 Награда: {10m:N0} ZZZ", result.Message);
        Assert.Contains($"⭐ Следующий рубеж: {1500m:N0}", result.Message);
    }

    [Fact]
    public async Task GlobalGoals_QueryFails_ReturnsError()
    {
        _m.GlobalGoalQuery
            .Setup(s => s.GetGoalsAsync())
            .ReturnsAsync(Result<List<GlobalGoalInfo>>.Fail("Не удалось загрузить"));

        var result = await _engine.ProcessInput(UserId, "/global_goals");

        Assert.NotNull(result.Message);
        Assert.Contains("Не удалось загрузить", result.Message);
    }

    [Fact]
    public async Task GlobalGoals_GroupChat_WorksWithoutButtons()
    {
        _m.GlobalGoalQuery
            .Setup(s => s.GetGoalsAsync())
            .ReturnsAsync(Result<List<GlobalGoalInfo>>.Ok(new List<GlobalGoalInfo>
            {
                new(1, "Цель", "Описание", 1000m, 500m, 0.5m, 0, new List<GlobalGoalStepInfo>())
            }));

        var result = await _engine.ProcessInput(UserId, "/global_goals", ChatType.Group);

        Assert.NotNull(result.Message);
        Assert.Contains("Цель", result.Message);
        Assert.Equal(ChatType.Group, result.ChatType);
        Assert.Null(result.Buttons);
    }

    // ═══════════════════════════════════════════════════════════
    //  /admin_global_goal_create
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminCreateGlobalGoal_ValidJson_CallsService()
    {
        _m.GlobalGoalCreation
            .Setup(s => s.CreateGoalAsync(It.IsAny<CreateGlobalGoalCommand>()))
            .ReturnsAsync(Result<long>.Ok(5));

        var start = await _engine.ProcessInput(UserId, "/admin_global_goal_create");
        Assert.NotNull(start.Message);
        Assert.Contains("Отправьте JSON, чтобы создать глобальную цель", start.Message);

        var result = await _engine.ProcessInput(UserId,
            "{\"name\":\"Общий баланс\",\"description\":\"Описание\",\"target\":1000000," +
            "\"symbolForReward\":\"ARK_001\",\"amountForReward\":10}");

        Assert.NotNull(result.Message);
        Assert.Contains("«Общий баланс» создана (ID: 5)", result.Message);

        _m.GlobalGoalCreation.Verify(
            s => s.CreateGoalAsync(It.Is<CreateGlobalGoalCommand>(c =>
                c.Name == "Общий баланс"
                && c.Description == "Описание"
                && c.Target == 1000000m
                && c.SymbolForReward == "ARK_001"
                && c.AmountForReward == 10m)),
            Times.Once);
    }

    [Fact]
    public async Task AdminCreateGlobalGoal_ServiceFails_ReturnsError()
    {
        _m.GlobalGoalCreation
            .Setup(s => s.CreateGoalAsync(It.IsAny<CreateGlobalGoalCommand>()))
            .ReturnsAsync(Result<long>.Fail("Имя цели не может быть пустым"));

        await _engine.ProcessInput(UserId, "/admin_global_goal_create");
        var result = await _engine.ProcessInput(UserId,
            "{\"name\":\"\",\"description\":\"Описание\",\"target\":1000000}");

        Assert.NotNull(result.Message);
        Assert.Contains("Имя цели не может быть пустым", result.Message);
    }

    [Fact]
    public async Task AdminCreateGlobalGoal_InvalidJson_ReturnsError()
    {
        await _engine.ProcessInput(UserId, "/admin_global_goal_create");
        var result = await _engine.ProcessInput(UserId, "not a json");

        Assert.NotNull(result.Message);
        Assert.Contains("Ошибка", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  /admin_global_goal_add_step
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminAddGoalStep_ValidJson_CallsService()
    {
        _m.GlobalGoalCreation
            .Setup(s => s.AddStepAsync(It.IsAny<AddGlobalGoalStepCommand>()))
            .ReturnsAsync(Result.Ok());

        var start = await _engine.ProcessInput(UserId, "/admin_global_goal_add_step");
        Assert.NotNull(start.Message);
        Assert.Contains("Отправьте JSON, чтобы добавить промежуточный шаг", start.Message);

        var result = await _engine.ProcessInput(UserId,
            "{\"goalId\":1,\"stepNumber\":1,\"target\":1500000,\"symbolForReward\":\"ZZZ\",\"amountForReward\":10}");

        Assert.NotNull(result.Message);
        Assert.Contains("✅ Шаг 1 добавлен к цели 1", result.Message);

        _m.GlobalGoalCreation.Verify(
            s => s.AddStepAsync(It.Is<AddGlobalGoalStepCommand>(c =>
                c.GoalId == 1
                && c.StepNumber == 1
                && c.Target == 1500000m
                && c.SymbolForReward == "ZZZ"
                && c.AmountForReward == 10m)),
            Times.Once);
    }

    [Fact]
    public async Task AdminAddGoalStep_ServiceFails_ReturnsError()
    {
        _m.GlobalGoalCreation
            .Setup(s => s.AddStepAsync(It.IsAny<AddGlobalGoalStepCommand>()))
            .ReturnsAsync(Result.Fail("Цель с ID 999 не найдена."));

        await _engine.ProcessInput(UserId, "/admin_global_goal_add_step");
        var result = await _engine.ProcessInput(UserId,
            "{\"goalId\":999,\"stepNumber\":1,\"target\":1500000,\"symbolForReward\":\"ZZZ\",\"amountForReward\":10}");

        Assert.NotNull(result.Message);
        Assert.Contains("Цель с ID 999 не найдена", result.Message);
    }
}