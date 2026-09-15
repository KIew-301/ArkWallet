using ArkWallet.Core.GlobalGoalContext.Application.Contracts.GlobalGoalServices;
using ArkWallet.Core.GlobalGoalContext.Application.Services.GlobalGoalServices;
using ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Core.TradingContext.Domain.TraderAggregate;
using ArkWallet.Core.TradingContext.Domain.TokenAggregate;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Core.TradingContext.Domain.TradeAggregate;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Core.PortfolioContext.Domain.Position;
using ArkWallet.Core.GiftContext.Domain.User;
using ArkWallet.Core.MailContext.Domain.Message;
using ArkWallet.Core.MiningContext.Domain.Machine;
using ArkWallet.Core.MiningContext.Domain.GlobalRule;
using ArkWallet.Core.MiningContext.Domain.Engines;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.Core.GlobalGoalContext.Application.Services.GlobalGoalServices;

public class GlobalGoalCreationServiceTest
{
    private static ArkWalletDbContext CreateDb()
        => DbTest.CreateInitializedDbContextAsync().GetAwaiter().GetResult();

    private static GlobalGoalCreationService BuildService(ArkWalletDbContext db,
        IEnumerable<IDomainGlobalGoalCalculation>? calculations = null)
        => new(db, calculations ?? Array.Empty<IDomainGlobalGoalCalculation>(),
            NullLogger<GlobalGoalCreationService>.Instance);

    [Fact]
    public async Task CreateGoalAsync_Valid_PersistsGoal()
    {
        using var db = CreateDb();
        var service = BuildService(db);

        var result = await service.CreateGoalAsync(
            new CreateGlobalGoalCommand("Общий баланс", "Описание цели", 1000m, "ZZZ", 10m));

        Assert.True(result.TryGetData(out var id));
        var record = db.GlobalGoals.Single();
        Assert.Equal(id, record.Id);
        Assert.Equal("Общий баланс", record.Name);
        Assert.Equal("Описание цели", record.Description);
        Assert.Equal(1000m, record.Target);
        Assert.Equal(0m, record.Actual);
        Assert.Equal(0m, record.Progress);
        Assert.Equal(0, record.AchievedCount);

        var step = Assert.Single(db.GlobalGoalSteps.ToList());
        Assert.Equal(record.Id, step.GoalId);
        Assert.Equal(1, step.StepNumber);
        Assert.Equal(1000m, step.Target);
        Assert.Equal("ZZZ", step.SymbolForReward);
        Assert.Equal(10m, step.AmountForReward);
    }

    [Fact]
    public async Task CreateGoalAsync_EmptyName_FailsWithoutPersist()
    {
        using var db = CreateDb();
        var service = BuildService(db);

        var result = await service.CreateGoalAsync(
            new CreateGlobalGoalCommand("", "Описание цели", 1000m, "ZZZ", 10m));

        Assert.False(result.IsSuccess);
        Assert.Empty(db.GlobalGoals.ToList());
        Assert.Empty(db.GlobalGoalSteps.ToList());
    }

    [Fact]
    public async Task CreateGoalAsync_ComputesInitialActualFromCalculation()
    {
        using var db = CreateDb();
        var calculation = new FakeGlobalGoalCalculation("Общий баланс", _ => Task.FromResult(250m));
        var service = BuildService(db, new[] { calculation });

        var result = await service.CreateGoalAsync(
            new CreateGlobalGoalCommand("Общий баланс", "Описание цели", 1000m, "ZZZ", 10m));

        Assert.True(result.TryGetData(out var id));
        var record = db.GlobalGoals.Single();
        Assert.Equal(250m, record.Actual);
        Assert.Equal(0.25m, record.Progress);
    }

    private sealed class FakeGlobalGoalCalculation : IDomainGlobalGoalCalculation
    {
        private readonly Func<ArkWalletDbContext, Task<decimal>> _calculator;

        public FakeGlobalGoalCalculation(string name, Func<ArkWalletDbContext, Task<decimal>> calculator)
        {
            GoalName = name;
            _calculator = calculator;
        }

        public string GoalName { get; }

        public Task<decimal> CalculateAsync(ArkWalletDbContext dbContext) => _calculator(dbContext);
    }

    [Fact]
    public async Task CreateGoalAsync_TrimsWhitespace()
    {
        using var db = CreateDb();
        var service = BuildService(db);

        var result = await service.CreateGoalAsync(
            new CreateGlobalGoalCommand("  Общий баланс  ", "  Описание  ", 1000m, "ZZZ", 10m));

        Assert.True(result.IsSuccess);
        Assert.Equal("Общий баланс", db.GlobalGoals.Single().Name);
        Assert.Equal("Описание", db.GlobalGoals.Single().Description);
    }

    [Fact]
    public async Task AddStepAsync_Valid_PersistsStep()
    {
        using var db = CreateDb();
        db.GlobalGoals.Add(GlobalGoal.Create(1, "Общий баланс", "Описание", 1000m, 0m, 0m, 0));
        await db.SaveChangesAsync();

        var service = BuildService(db);
        var result = await service.AddStepAsync(
            new AddGlobalGoalStepCommand(1, 1, 1500m, "ZZZ", 10m));

        Assert.True(result.IsSuccess);
        var step = db.GlobalGoalSteps.Single();
        Assert.Equal(1, step.GoalId);
        Assert.Equal(1, step.StepNumber);
        Assert.Equal(1500m, step.Target);
        Assert.Equal("ZZZ", step.SymbolForReward);
        Assert.Equal(10m, step.AmountForReward);
    }

    [Fact]
    public async Task AddStepAsync_GoalNotFound_Fails()
    {
        using var db = CreateDb();
        var service = BuildService(db);

        var result = await service.AddStepAsync(
            new AddGlobalGoalStepCommand(999, 1, 1500m, "ZZZ", 10m));

        Assert.False(result.IsSuccess);
        Assert.Empty(db.GlobalGoalSteps.ToList());
    }

    [Fact]
    public async Task AddStepAsync_DuplicateStepNumber_Fails()
    {
        using var db = CreateDb();
        db.GlobalGoals.Add(GlobalGoal.Create(1, "Общий баланс", "Описание", 1000m, 0m, 0m, 0));
        db.GlobalGoalSteps.Add(GlobalGoalStep.Create(1, 1, 1500m, "ZZZ", 10m));
        await db.SaveChangesAsync();

        var service = BuildService(db);
        var result = await service.AddStepAsync(
            new AddGlobalGoalStepCommand(1, 1, 2000m, "AAA", 5m));

        Assert.False(result.IsSuccess);
        Assert.Single(db.GlobalGoalSteps.ToList());
    }

    [Fact]
    public async Task AddStepAsync_InvalidStepNumber_Fails()
    {
        using var db = CreateDb();
        db.GlobalGoals.Add(GlobalGoal.Create(1, "Общий баланс", "Описание", 1000m, 0m, 0m, 0));
        await db.SaveChangesAsync();

        var service = BuildService(db);
        var result = await service.AddStepAsync(
            new AddGlobalGoalStepCommand(1, 0, 1500m, "ZZZ", 10m));

        Assert.False(result.IsSuccess);
        Assert.Empty(db.GlobalGoalSteps.ToList());
    }
}