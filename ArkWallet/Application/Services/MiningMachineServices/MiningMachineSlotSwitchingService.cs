using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MiningMachineServices;
using static Result<MiningTokenCollectionResult>;

internal class MiningMachineSlotSwitchingService(
    ArkWalletDbContext dbContext,
    ILogger<MiningMachineSlotSwitchingService> logger,
    TimeProvider? timeProvider = null) : IMiningMachineSlotSwitchingService
{
    public async Task<Result<MiningTokenCollectionResult>> SwitchTargetTokenAsync(long traderId, long miningMachineSlotId, string symbol)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockTradersAsync([traderId]);

                var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderId);
                if (trader == null)
                    return Fail("Трейдера не существует");

                var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == symbol);
                if (token == null)
                    return Fail("Токена не существует");

                var slot = await dbContext.MiningMachineSlots
                    .Include(s => s.MiningMachine)
                    .ThenInclude(m => m!.MiningMachineRules)
                    .FirstOrDefaultAsync(s => s.Id == miningMachineSlotId);
                if (slot == null)
                    return Fail("Слота не существует");

                var machineRule = slot.MiningMachine?.MiningMachineRules
                    .FirstOrDefault(r => r.CharacterTokenId == symbol);
                if (machineRule == null)
                    return Fail("Правила для такой связки машины и токена не существует");

                var globalRule = await dbContext.MiningGlobalRules.FirstOrDefaultAsync(r => r.TokenId == symbol);
                if (globalRule == null)
                    return Fail("Глобального правила для токена не существует");

                var collected = slot.CollectWholeTokens();
                var oldSymbol = slot.TokenId ?? string.Empty;

                var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
                slot.SwitchTargetToken(
                    traderId,
                    symbol,
                    machineRule.Id,
                    globalRule.Id,
                    slot.MiningMachine!.SwitchingTime,
                    now);

                await dbContext.SaveChangesAsync();

                return Ok(new(oldSymbol, collected));
            });
        }, logger, nameof(MiningMachineSlotSwitchingService));
    }

    public async Task<Result<int>> CheckSwitchingAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

                var switchingSlots = await dbContext.MiningMachineSlots
                    .Where(s => s.Status == MiningMachineSlotStatus.Switching)
                    .ToListAsync();

                var completed = 0;
                foreach (var slot in switchingSlots)
                {
                    if (slot.EndSwitchingDateTime <= now)
                    {
                        slot.CompleteSwitching();
                        completed++;
                    }
                }

                await dbContext.SaveChangesAsync();

                return Result<int>.Ok(completed);
            });
        }, logger, nameof(MiningMachineSlotSwitchingService));
    }
}
