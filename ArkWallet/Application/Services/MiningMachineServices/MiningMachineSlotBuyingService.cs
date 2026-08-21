using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Engines;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MiningMachineServices;
using static Result<long>;

internal class MiningMachineSlotBuyingService(
    ArkWalletDbContext dbContext,
    ILogger<MiningMachineSlotBuyingService> logger,
    TimeProvider? timeProvider = null) : IMiningMachineSlotBuyingService
{
    public async Task<Result<long>> BuyMachineAsync(long traderId, long machineId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockTradersAsync([traderId]);
                await dbContext.LockMiningMachinesAsync([machineId]);

                var purchaseResult = await EnsurePurchaseAllowedAsync(traderId, machineId);
                if (!purchaseResult.IsSuccess || !purchaseResult.TryGetData(out var purchase))
                    return Fail(purchaseResult.Message);

                var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

                var slot = MiningMachineSlot.Create(
                    traderId,
                    purchase.Machine,
                    purchase.Machine.GetSellingPrice(),
                    now);
                await dbContext.MiningMachineSlots.AddAsync(slot);

                purchase.Trader.AddToBalance(-purchase.Machine.Cost);
                await dbContext.SaveChangesAsync();

                return Ok(slot.Id);
            });
        }, logger, nameof(MiningMachineSlotBuyingService));
    }

    /// <summary>Data required to complete a machine purchase.</summary>
    private sealed record PurchaseContext(Trader Trader, MiningMachine Machine);

    /// <summary>Loads the trader and machine and verifies every precondition for buying the machine.</summary>
    private async Task<Result<PurchaseContext>> EnsurePurchaseAllowedAsync(long traderId, long machineId)
    {
        var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderId);
        if (trader == null)
            return Result<PurchaseContext>.Fail("Трейдера не существует");

        var machine = await dbContext.MiningMachines
            .Include(m => m.MiningMachineRules)
            .FirstOrDefaultAsync(m => m.Id == machineId);
        if (machine == null)
            return Result<PurchaseContext>.Fail("Машины не существует");
        if (!machine.IsActiveForSale)
            return Result<PurchaseContext>.Fail("Машина недоступна для покупки");

        if (await IsMachineAlreadyOwnedAsync(traderId, machine.Name))
            return Result<PurchaseContext>.Fail("У вас уже есть такая машина");

        if (await HasReachedMachinesLimitAsync(traderId))
            return Result<PurchaseContext>.Fail($"Нельзя купить больше {MiningEngine.MaxMachinesPerTrader} машин");

        if (!trader.CanAfford(machine.Cost))
            return Result<PurchaseContext>.Fail("Недостаточно средств для покупки машины");

        return Result<PurchaseContext>.Ok(new PurchaseContext(trader, machine));
    }

    /// <summary>Determines whether the trader already owns an unsold slot with the given machine name.</summary>
    private async Task<bool> IsMachineAlreadyOwnedAsync(long traderId, string machineName)
    {
        return await dbContext.MiningMachineSlots.AnyAsync(s =>
            s.TraderId == traderId && s.Name == machineName && s.Status != MiningMachineSlotStatus.Sold);
    }

    /// <summary>Determines whether the trader has reached the maximum number of owned machines.</summary>
    private async Task<bool> HasReachedMachinesLimitAsync(long traderId)
    {
        var slotsCount = await dbContext.MiningMachineSlots.CountAsync(s =>
            s.TraderId == traderId && s.Status != MiningMachineSlotStatus.Sold);
        return slotsCount >= MiningEngine.MaxMachinesPerTrader;
    }
}
