using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MiningMachineServices;
using static Result<MiningTokenCollectionResult>;

internal class MiningMachineSlotSellingService(
    ArkWalletDbContext dbContext,
    ILogger<MiningMachineSlotSellingService> logger,
    TimeProvider? timeProvider = null) : IMiningMachineSlotSellingService
{
    public async Task<Result<MiningTokenCollectionResult>> SellMachineAsync(long traderId, long miningMachineSlotId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockTradersAsync([traderId]);
                await dbContext.LockMiningMachineSlotsAsync([miningMachineSlotId]);

                var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderId);
                if (trader == null)
                    return Fail("Трейдера не существует");

                var slot = await dbContext.MiningMachineSlots.FirstOrDefaultAsync(s => s.Id == miningMachineSlotId);
                if (slot == null)
                    return Fail("Слота не существует");
                if (slot.TraderId != traderId)
                    return Fail("Трейдер не владеет данной машиной");

                var collected = slot.CollectWholeTokens();
                var symbol = slot.TokenId ?? string.Empty;

                var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
                slot.Sell(traderId, now);

                trader.AddToBalance(slot.Cost);
                trader.MarkDirty();
                await dbContext.SaveChangesAsync();

                return Ok(new(symbol, collected));
            });
        }, logger, nameof(MiningMachineSlotSellingService));
    }
}
