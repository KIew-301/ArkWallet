using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MailServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MailServices;

internal class MailQueryService(ArkWalletDbContext dbContext, ILogger<MailQueryService> logger) : IMailQueryService
{
    public async Task<Result<List<MailInfo>>> GetUserMailsAsync(long traderId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var mails = await dbContext.MailMessages
                .Where(m => m.TraderId == traderId)
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Select(m => new MailInfo(
                    m.Id,
                    m.TraderId,
                    m.Title,
                    m.Message,
                    m.SenderName,
                    m.SenderId,
                    m.SymbolForReward,
                    m.AmountForReward,
                    m.Status,
                    m.CreatedAt,
                    m.ReadAt,
                    m.AcceptedAt))
                .ToListAsync();

            return Result<List<MailInfo>>.Ok(mails);
        }, logger, nameof(MailQueryService));
    }
}
