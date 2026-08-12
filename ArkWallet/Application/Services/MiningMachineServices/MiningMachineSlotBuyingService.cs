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

                var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderId);
                if (trader == null)
                    return Fail("Трейдера не существует");

                var machine = await dbContext.MiningMachines.FirstOrDefaultAsync(m => m.Id == machineId);
                if (machine == null)
                    return Fail("Машины не существует");
                if (!machine.IsActiveForSale)
                    return Fail("Машина недоступна для покупки");

                var slotsCount = await dbContext.MiningMachineSlots.CountAsync(s =>
                    s.TraderId == traderId && s.Status != MiningMachineSlotStatus.Sold);
                if (slotsCount >= MiningEngine.MaxMachinesPerTrader)
                    return Fail($"Нельзя купить больше {MiningEngine.MaxMachinesPerTrader} машин");

                var resalePrice = machine.GetSellingPrice();
                if (!trader.CanAfford(machine.Cost))
                    return Fail("Недостаточно средств для покупки машины");

                var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

                var slot = MiningMachineSlot.Create(traderId, machineId, resalePrice, now);
                await dbContext.MiningMachineSlots.AddAsync(slot);

                trader.AddToBalance(-machine.Cost);
                trader.MarkDirty();
                await dbContext.SaveChangesAsync();

                return Ok(slot.Id);
            });
        }, logger, nameof(MiningMachineSlotBuyingService));
    }
}
