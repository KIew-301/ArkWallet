using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Application.Services.GiftServices;
using ArkWallet.Domain.Common;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ArkWallet.Tests.ConcurrencyTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;

    public string ConnectionString { get; private set; } = string.Empty;

    public bool IsAvailable { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("arkwallet_test")
                .WithUsername("arkwallet")
                .WithPassword("arkwallet")
                .Build();

            await _postgres.StartAsync();
            ConnectionString = _postgres.GetConnectionString();
            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }
}

public sealed class ConcurrencyLockTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private ArkWalletDbContext CreateContext()
    {
        Skip.If(!fixture.IsAvailable, "Docker/Postgres недоступен — RC-тест пропущен");

        var options = new DbContextOptionsBuilder<ArkWalletDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        return new ArkWalletDbContext(options);
    }

    [SkippableFact]
    public async Task TraderLock_BlocksConcurrentWriter_UntilReleased()
    {
        await using (var db = CreateContext())
            await db.Database.EnsureCreatedAsync();

        await using (var seed = CreateContext())
        {
            seed.Traders.Add(Trader.Create(101, "Seller"));
            await seed.SaveChangesAsync();
        }

        await using var holder = CreateContext();
        await using var waiter = CreateContext();

        await using var holderTx = await holder.Database.BeginTransactionAsync();
        await holder.LockTradersAsync([101L]);

        await using var waiterTx = await waiter.Database.BeginTransactionAsync();
        await waiter.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '300ms'");

        var blocked = await Assert.ThrowsAsync<PostgresException>(
            () => waiter.LockTradersAsync([101L]));
        Assert.Equal("55P03", blocked.SqlState);

        await waiterTx.RollbackAsync();

        await holderTx.CommitAsync();

        await using var waiterTx2 = await waiter.Database.BeginTransactionAsync();
        await waiter.LockTradersAsync([101L]);
        await waiterTx2.CommitAsync();
    }

    [SkippableFact]
    public async Task MiningSlotLock_BlocksConcurrentWriter_UntilReleased()
    {
        await using (var db = CreateContext())
            await db.Database.EnsureCreatedAsync();

        long slotId;
        await using (var seed = CreateContext())
        {
            await HelpMethods.RegisterTrader(seed, 201);

            var machine = MiningMachine.Create(MiningMachineType.SMAI, 10, 80, true, "img.zzz", 1m);
            seed.MiningMachines.Add(machine);
            await seed.SaveChangesAsync();

            var slot = MiningMachineSlot.Create(201, machine, 1200, DateTime.UtcNow);
            seed.MiningMachineSlots.Add(slot);
            await seed.SaveChangesAsync();

            slotId = slot.Id;
        }

        await using var holder = CreateContext();
        await using var waiter = CreateContext();

        await using var holderTx = await holder.Database.BeginTransactionAsync();
        await holder.LockMiningMachineSlotsAsync([slotId]);

        await using var waiterTx = await waiter.Database.BeginTransactionAsync();
        await waiter.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '300ms'");

        var blocked = await Assert.ThrowsAsync<PostgresException>(
            () => waiter.LockMiningMachineSlotsAsync([slotId]));
        Assert.Equal("55P03", blocked.SqlState);

        await waiterTx.RollbackAsync();
        await holderTx.CommitAsync();

        await using var waiterTx2 = await waiter.Database.BeginTransactionAsync();
        await waiter.LockMiningMachineSlotsAsync([slotId]);
        await waiterTx2.CommitAsync();
    }

    [SkippableFact]
    public async Task ActiveMiningSlotLock_BlocksConcurrentWorker_UntilReleased()
    {
        await using (var db = CreateContext())
            await db.Database.EnsureCreatedAsync();

        await using (var seed = CreateContext())
        {
            await HelpMethods.RegisterTrader(seed, 301);

            var machine = MiningMachine.Create(MiningMachineType.SMAI, 30, 80, true, "img.zzz", 1m);
            seed.MiningMachines.Add(machine);
            await seed.SaveChangesAsync();

            var slot = MiningMachineSlot.Create(301, machine, 1200, DateTime.UtcNow);
            seed.MiningMachineSlots.Add(slot);
            await seed.SaveChangesAsync();

            await seed.Database.ExecuteSqlRawAsync(
                "UPDATE \"MiningMachineSlots\" SET \"Status\" = 'Active' WHERE \"Id\" = {0}", slot.Id);
        }

        await using var holder = CreateContext();
        await using var waiter = CreateContext();

        await using var holderTx = await holder.Database.BeginTransactionAsync();
        await holder.LockActiveMiningMachineSlotsAsync();

        await using var waiterTx = await waiter.Database.BeginTransactionAsync();
        await waiter.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '300ms'");

        var blocked = await Assert.ThrowsAsync<PostgresException>(
            () => waiter.LockActiveMiningMachineSlotsAsync());
        Assert.Equal("55P03", blocked.SqlState);

        await waiterTx.RollbackAsync();
        await holderTx.CommitAsync();
    }

    [SkippableFact]
    public async Task MiningMachineLock_BlocksConcurrentWriter_UntilReleased()
    {
        await using (var db = CreateContext())
            await db.Database.EnsureCreatedAsync();

        long machineId;
        await using (var seed = CreateContext())
        {
            var machine = MiningMachine.Create(MiningMachineType.SMAI, 150, 80, true, "img.zzz", 1m);
            seed.MiningMachines.Add(machine);
            await seed.SaveChangesAsync();

            machineId = machine.Id;
        }

        await using var holder = CreateContext();
        await using var waiter = CreateContext();

        await using var holderTx = await holder.Database.BeginTransactionAsync();
        await holder.LockMiningMachinesAsync([machineId]);

        await using var waiterTx = await waiter.Database.BeginTransactionAsync();
        await waiter.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '300ms'");

        var blocked = await Assert.ThrowsAsync<PostgresException>(
            () => waiter.LockMiningMachinesAsync([machineId]));
        Assert.Equal("55P03", blocked.SqlState);

        await waiterTx.RollbackAsync();
        await holderTx.CommitAsync();
    }

    [SkippableFact]
    public async Task ConcurrentBuyers_DoNotDoubleFillSellerOrder()
    {
        await using (var db = CreateContext())
            await db.Database.EnsureCreatedAsync();

        await using (var seed = CreateContext())
        {
            await HelpMethods.RegisterTrader(seed, 1002, "Buyer1");
            await HelpMethods.RegisterTrader(seed, 1003, "Buyer2");
        }

        const int iterations = 30;

        for (var i = 0; i < iterations; i++)
        {
            var symbol = $"ZZZ{i}";
            var sellerId = 2000 + i;

            await using (var seed = CreateContext())
            {
                await HelpMethods.RegisterTrader(seed, sellerId, $"Seller{i}");
                await HelpMethods.CreateToken(seed, symbol);
                await HelpMethods.AddPortfolio(seed, sellerId, symbol, 100);
                await HelpMethods.GiveMoney(seed, 1002, 50_000m);
                await HelpMethods.GiveMoney(seed, 1003, 50_000m);

                var sell = await HelpMethods.PlaceOrder(seed, sellerId, "продать", symbol, 100, 100);
                Assert.True(sell.IsSuccess, sell.Message);
            }

            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var task1 = Task.Run(async () =>
            {
                await start.Task;
                await using var db = CreateContext();
                return await BuyAsync(db, 1002, symbol);
            });
            var task2 = Task.Run(async () =>
            {
                await start.Task;
                await using var db = CreateContext();
                return await BuyAsync(db, 1003, symbol);
            });

            start.SetResult();
            await Task.WhenAll(task1, task2);

            await using var check = CreateContext();
            var sellerOrder = await check.TradeOrders
                .SingleAsync(o => o.TraderTelegramId == sellerId && o.CharacterTokenId == symbol);

            Assert.Equal(100, sellerOrder.FilledQuantity);

            var buyersExecuted = await check.TradeOrders
                .Where(o => (o.TraderTelegramId == 1002 || o.TraderTelegramId == 1003) && o.CharacterTokenId == symbol)
                .SumAsync(o => o.FilledQuantity);

            Assert.Equal(100, buyersExecuted);
        }
    }

    [SkippableFact]
    public async Task GiftSend_BlocksConcurrentWriter_UntilReleased()
    {
        await using (var db = CreateContext())
            await db.Database.EnsureCreatedAsync();

        await using (var seed = CreateContext())
        {
            await HelpMethods.RegisterTrader(seed, 101);
            await HelpMethods.RegisterTrader(seed, 102);
        }

        await using var holder = CreateContext();
        await using var waiter = CreateContext();

        await using var holderTx = await holder.Database.BeginTransactionAsync();
        await holder.LockTradersAsync([101L]);

        await using var waiterTx = await waiter.Database.BeginTransactionAsync();
        await waiter.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '300ms'");

        var service = new GiftSendingService(
            waiter,
            new Mock<ITokenQueryService>().Object,
            new Mock<IEventPublisher>().Object,
            NullLogger<GiftSendingService>.Instance,
            new TestTimeProvider());

        var blocked = await Assert.ThrowsAsync<PostgresException>(
            () => service.SendGiftAsync(101, 102));
        Assert.Equal("55P03", blocked.SqlState);

        await waiterTx.RollbackAsync();
        await holderTx.CommitAsync();
    }

    [SkippableFact]
    public async Task GiftReceive_BlocksConcurrentWriter_UntilReleased()
    {
        await using (var db = CreateContext())
            await db.Database.EnsureCreatedAsync();

        await using (var seed = CreateContext())
        {
            await HelpMethods.RegisterTrader(seed, 102);
        }

        await using var holder = CreateContext();
        await using var waiter = CreateContext();

        await using var holderTx = await holder.Database.BeginTransactionAsync();
        await holder.LockTradersAsync([102L]);

        await using var waiterTx = await waiter.Database.BeginTransactionAsync();
        await waiter.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '300ms'");

        var service = new GiftReceivingService(
            waiter,
            new Mock<IEventPublisher>().Object,
            NullLogger<GiftReceivingService>.Instance,
            new TestTimeProvider());

        var blocked = await Assert.ThrowsAsync<PostgresException>(
            () => service.ReceiveGiftAsync(102, Guid.NewGuid()));
        Assert.Equal("55P03", blocked.SqlState);

        await waiterTx.RollbackAsync();
        await holderTx.CommitAsync();
    }

    private static async Task<Result<OrderCreationData>> BuyAsync(ArkWalletDbContext db, long traderId, string symbol)
    {
        var service = BuildOrderService(db);
        return await service.CreateOrderAsync(new CreateOrderCommand(traderId, "купить", symbol, 100, 100));
    }

    private static OrderCreationService BuildOrderService(ArkWalletDbContext db)
    {
        var engine = new TradingEngine();
        var validator = new Mock<IOrderValidationService>();
        validator
            .Setup(x => x.ValidateFullOrderAsync(It.IsAny<CreateOrderCommand>()))
            .ReturnsAsync(new ValidationResult(true));
        var dispatcher = new Mock<ITaskDispatcher>();
        dispatcher.Setup(x => x.SendTaskAsync(It.IsAny<string>(), It.IsAny<object>()));
        var candle = new Mock<ITokenPriceCandleUpdateService>();
        candle
            .Setup(x => x.UpdateTokenPriceCandleAsync(It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync(new Result(true, "Success"));
        var logger = NullLogger<OrderCreationService>.Instance;

        return new OrderCreationService(
            db,
            engine,
            validator.Object,
            new MediatREventPublisher(TestMediatorFactory.Create(db, candle.Object)),
            dispatcher.Object,
            logger);
    }
}
