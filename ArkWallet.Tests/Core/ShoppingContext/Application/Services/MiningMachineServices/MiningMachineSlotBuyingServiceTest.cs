using ArkWallet.Core.ShoppingContext.Application.Services.MiningMachineServices;
using ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Core.TradingContext.Domain.TraderAggregate;
using ArkWallet.Core.TradingContext.Domain.TokenAggregate;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Core.TradingContext.Domain.TradeAggregate;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Core.PortfolioContext.Domain.Position;
using ArkWallet.Core.GiftContext.Domain.User;
using ArkWallet.Core.MailContext.Domain.Message;
using ArkWallet.Core.MiningContext.Application.Services.MiningMachineServices;
using ArkWallet.Core.MiningContext.Domain.Machine;
using ArkWallet.Core.MiningContext.Domain.GlobalRule;
using ArkWallet.Core.MiningContext.Domain.Engines;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.Core.ShoppingContext.Application.Services.MiningMachineServices;

public class MiningMachineSlotBuyingServiceTest
{
    private static MachineSlotBuyingService CreateService(ArkWalletDbContext db) =>
        new(db, NullLogger<MachineSlotBuyingService>.Instance);

    private static readonly decimal[] CategoryEfficiencies =
        [0.003m, 0.006m, 0.011m, 0.018m, 0.033m, 0.046m, 0.084m, 0.157m, 0.373m, 0.763m, 1.476m];

    private static MiningMachine CreateMachine(
        ArkWalletDbContext db,
        decimal efficiency = 1m,
        decimal reusability = 80,
        bool isActiveForSale = true)
    {
        var machine = MiningMachine.Create(
            MiningMachineType.SMAI, 10, reusability, isActiveForSale, "img.zzz", efficiency);
        db.MiningMachines.Add(machine);
        return machine;
    }

    [Fact]
    public async Task BuyMachineAsync_ValidPurchase_ChargesFullCostAndSavesResalePrice()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 2000);
        Assert.True(trader.IsSuccess, trader.Message);

        var machine = CreateMachine(db, reusability: 80);
        await db.SaveChangesAsync();
        await HelpMethods.GiveMoney(db, 2000, machine.Cost + 1000);

        var result = await CreateService(db).BuyMachineAsync(2000, machine.Id);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var slotId));

        var traderEntity = await HelpMethods.GetTrader(db, 2000);
        Assert.Equal(2000m, traderEntity!.Balance);

        var slot = await db.MiningMachineSlots.FindAsync(slotId);
        Assert.NotNull(slot);
        Assert.Equal(machine.Name, slot!.Name);
        Assert.Equal(machine.Type, slot.Type);
        Assert.Equal(machine.SwitchingTime, slot.SwitchingTime);
        Assert.Equal(machine.Efficiency, slot.Efficiency);
        Assert.Equal(machine.Image, slot.Image);
        Assert.Equal(
            ArkWallet.Core.ShoppingContext.Domain.Machine.Machine.CalculateSellingPrice(machine.Cost, machine.Reusability),
            slot.Cost);
        Assert.Equal(MiningMachineSlotStatus.Passive, slot.Status);
        Assert.Empty(await db.MiningMachineSlotRules.Where(r => r.MiningMachineSlotId == slot.Id).ToListAsync());
    }

    [Fact]
    public async Task BuyMachineAsync_WithRules_CopiesRulesToSlot()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 2000);
        Assert.True(trader.IsSuccess, trader.Message);

        await HelpMethods.CreateToken(db, "AAA");
        await HelpMethods.CreateToken(db, "BBB");

        var machine = MiningMachine.Create(MiningMachineType.MGC, 30, 80, true, "img.zzz", 0.5m);
        machine.MiningMachineRules.Add(MiningMachineRule.Create(0, "AAA", 0.9m));
        machine.MiningMachineRules.Add(MiningMachineRule.Create(0, "BBB", 0.7m));
        db.MiningMachines.Add(machine);
        await db.SaveChangesAsync();
        await HelpMethods.GiveMoney(db, 2000, machine.Cost + 1000);

        var result = await CreateService(db).BuyMachineAsync(2000, machine.Id);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var slotId));

        var rules = await db.MiningMachineSlotRules
            .Where(r => r.MiningMachineSlotId == slotId)
            .OrderBy(r => r.CharacterTokenId)
            .ToListAsync();
        Assert.Collection(rules,
            r => { Assert.Equal("AAA", r.CharacterTokenId); Assert.Equal(0.9m, r.MiningCoefficient); },
            r => { Assert.Equal("BBB", r.CharacterTokenId); Assert.Equal(0.7m, r.MiningCoefficient); });
    }

    [Fact]
    public async Task BuyMachineAsync_InsufficientFunds_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 2000);
        Assert.True(trader.IsSuccess, trader.Message);
        await HelpMethods.GiveMoney(db, 2000, 300);

        var machine = CreateMachine(db);
        await db.SaveChangesAsync();

        var result = await CreateService(db).BuyMachineAsync(2000, machine.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("Недостаточно средств", result.Message);
    }

    [Fact]
    public async Task BuyMachineAsync_MoreThanFiveMachines_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 2000);
        Assert.True(trader.IsSuccess, trader.Message);
        await HelpMethods.GiveMoney(db, 2000, 100000000);

        var machines = new List<MiningMachine>();
        for (var i = 0; i < 6; i++)
            machines.Add(CreateMachine(db, efficiency: CategoryEfficiencies[i]));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        for (var i = 0; i < 5; i++)
        {
            var result = await service.BuyMachineAsync(2000, machines[i].Id);
            Assert.True(result.IsSuccess, $"Purchase #{i}: {result.Message}");
        }

        var sixth = await service.BuyMachineAsync(2000, machines[5].Id);

        Assert.False(sixth.IsSuccess);
        Assert.Contains("5", sixth.Message);
        Assert.Equal(5, await db.MiningMachineSlots.CountAsync(s => s.TraderId == 2000));
    }

    [Fact]
    public async Task BuyMachineAsync_TenMachinesButOneSold_AllowsPurchase()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 2000);
        Assert.True(trader.IsSuccess, trader.Message);
        await HelpMethods.GiveMoney(db, 2000, 100000000);

        var machines = new List<MiningMachine>();
        for (var i = 0; i < 6; i++)
            machines.Add(CreateMachine(db, efficiency: CategoryEfficiencies[i]));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        for (var i = 0; i < 5; i++)
        {
            var result = await service.BuyMachineAsync(2000, machines[i].Id);
            Assert.True(result.IsSuccess, result.Message);
        }

        var soldSlot = await db.MiningMachineSlots.FirstAsync(s => s.TraderId == 2000);
        var sm = MiningContextMapper.MachineFrom(soldSlot);
        sm.Sell(new TestTimeProvider());
        soldSlot.Update(sm);
        await db.SaveChangesAsync();

        var afterSell = await service.BuyMachineAsync(2000, machines[5].Id);

        Assert.True(afterSell.IsSuccess, afterSell.Message);
        Assert.Equal(5, await db.MiningMachineSlots.CountAsync(s => s.TraderId == 2000 && s.Status != MiningMachineSlotStatus.Sold));
    }

    [Fact]
    public async Task BuyMachineAsync_AlreadyOwned_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 2000);
        Assert.True(trader.IsSuccess, trader.Message);
        await HelpMethods.GiveMoney(db, 2000, 10000);

        var machine = CreateMachine(db, efficiency: 0.003m);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var first = await service.BuyMachineAsync(2000, machine.Id);
        Assert.True(first.IsSuccess, first.Message);

        var second = await service.BuyMachineAsync(2000, machine.Id);

        Assert.False(second.IsSuccess);
        Assert.Contains("уже есть такая машина", second.Message);
        Assert.Single(await db.MiningMachineSlots.Where(s => s.TraderId == 2000).ToListAsync());
    }

    [Fact]
    public async Task BuyMachineAsync_MachineNotForSale_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 2000);
        Assert.True(trader.IsSuccess, trader.Message);
        await HelpMethods.GiveMoney(db, 2000, 10000);

        var machine = CreateMachine(db, isActiveForSale: false);
        await db.SaveChangesAsync();

        var result = await CreateService(db).BuyMachineAsync(2000, machine.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("недоступна", result.Message);
    }

    [Fact]
    public async Task BuyMachineAsync_MachineNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 2000);
        Assert.True(trader.IsSuccess, trader.Message);
        await HelpMethods.GiveMoney(db, 2000, 10000);

        var result = await CreateService(db).BuyMachineAsync(2000, 999);

        Assert.False(result.IsSuccess);
        Assert.Contains("не существует", result.Message);
    }

    [Fact]
    public async Task BuyMachineAsync_TraderNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var machine = CreateMachine(db);
        await db.SaveChangesAsync();

        var result = await CreateService(db).BuyMachineAsync(999, machine.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("Трейдера не существует", result.Message);
    }

    [Fact]
    public async Task BuyMachineAsync_BalanceNotChangedOnFailure()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 2000);
        Assert.True(trader.IsSuccess, trader.Message);
        await HelpMethods.GiveMoney(db, 2000, 300);

        var machine = CreateMachine(db);
        await db.SaveChangesAsync();

        await CreateService(db).BuyMachineAsync(2000, machine.Id);

        var traderEntity = await HelpMethods.GetTrader(db, 2000);
        Assert.Equal(1300m, traderEntity!.Balance);
        Assert.Empty(await db.MiningMachineSlots.ToListAsync());
    }

    [Fact]
    public async Task BuyMachine_WithActiveSubscription_AllowsMoreThanFreeTierLimit()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 2001);
        Assert.True(trader.IsSuccess, trader.Message);
        await HelpMethods.GiveMoney(db, 2001, 100000000);

        var sub = new ArkWallet.Infrastructure.Data.Subscription
        {
            Id = 88,
            Name = "Pro",
            Level = 1,
            PriceRubles = 100m,
            MaxOrders = 10,
            MaxMiningMachines = 7,
            DurationMinutes = 43200
        };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var traderEntity = await HelpMethods.GetTrader(db, 2001);
        traderEntity.SubscriptionId = sub.Id;
        traderEntity.SubscriptionExpiresAtUtc = new DateTime(2999, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var machines = new List<MiningMachine>();
        for (var i = 0; i < 6; i++)
            machines.Add(CreateMachine(db, efficiency: CategoryEfficiencies[i]));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        for (var i = 0; i < 6; i++)
        {
            var result = await service.BuyMachineAsync(2001, machines[i].Id);
            Assert.True(result.IsSuccess, $"Purchase #{i + 1}: {result.Message}");
        }

        Assert.Equal(6, await db.MiningMachineSlots.CountAsync(s => s.TraderId == 2001));
    }
}
