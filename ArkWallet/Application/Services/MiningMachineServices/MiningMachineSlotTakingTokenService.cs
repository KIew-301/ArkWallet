using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MiningMachineServices;
using static Result<MiningTokenCollectionResult>;

internal class MiningMachineSlotTakingTokenService(
    ArkWalletDbContext dbContext,
    ILogger<MiningMachineSlotTakingTokenService> logger) : IMiningMachineSlotTakingTokenService
{
    public async Task<Result<MiningTokenCollectionResult>> TakeTokensFromMachineAsync(long traderId, long miningMachineSlotId)
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

                await dbContext.SaveChangesAsync();

                return Ok(new(symbol, collected));
            });
        }, logger, nameof(MiningMachineSlotTakingTokenService));
    }

    public async Task<Result<List<MiningTokenCollectionResult>>> TakeTokensFromMachinesAsync(long traderId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockTradersAsync([traderId]);

                var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderId);
                if (trader == null)
                    return Result<List<MiningTokenCollectionResult>>.Fail("Трейдера не существует");

                var slots = await LoadActiveTraderSlotsAsync(traderId);

                var results = CollectWholeTokens(slots);

                await dbContext.SaveChangesAsync();

                return Result<List<MiningTokenCollectionResult>>.Ok(results);
            });
        }, logger, nameof(MiningMachineSlotTakingTokenService));
    }

    /// <summary>Loads the trader's non-sold slots, locking them by identifier first.</summary>
    private async Task<List<MiningMachineSlot>> LoadActiveTraderSlotsAsync(long traderId)
    {
        var slotIds = await dbContext.MiningMachineSlots
            .Where(s => s.TraderId == traderId && s.Status != MiningMachineSlotStatus.Sold)
            .Select(s => s.Id)
            .ToListAsync();

        await dbContext.LockMiningMachineSlotsAsync(slotIds);

        return await dbContext.MiningMachineSlots
            .Where(s => s.TraderId == traderId && s.Status != MiningMachineSlotStatus.Sold)
            .ToListAsync();
    }

    /// <summary>Collects whole tokens from each slot, skipping slots without a token or without collected tokens.</summary>
    private static List<MiningTokenCollectionResult> CollectWholeTokens(List<MiningMachineSlot> slots)
    {
        var results = new List<MiningTokenCollectionResult>();
        foreach (var slot in slots)
        {
            var symbol = slot.TokenId;
            if (string.IsNullOrEmpty(symbol))
                continue;

            var collected = slot.CollectWholeTokens();
            if (collected > 0)
                results.Add(new(symbol, collected));
        }

        return results;
    }
}
