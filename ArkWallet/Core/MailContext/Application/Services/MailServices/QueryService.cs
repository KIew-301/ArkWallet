using System.Linq.Expressions;
using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.MailContext.Application.Contracts.MailServices;
using ArkWallet.Core.MailContext.Domain.Message;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.MailContext.Application.Services.MailServices;

internal class QueryService(ArkWalletDbContext dbContext, ILogger<QueryService> logger) : IQueryService
{
    private static readonly Expression<Func<MailMessage, MailInfo>> ToMailInfo = m => new MailInfo(
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
        m.AcceptedAt);

    public async Task<Result<List<MailInfo>>> GetUserMailsAsync(long traderId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var mails = await dbContext.MailMessages
                .Where(m => m.TraderId == traderId)
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Select(ToMailInfo)
                .ToListAsync();

            return Result<List<MailInfo>>.Ok(mails);
        }, logger, nameof(QueryService));
    }

    public async Task<Result<PagedResult<MailInfo>>> GetUserMailsPageAsync(
        long traderId,
        MailFilter filter,
        int page,
        int pageSize)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var (sent, read) = StatusValues();
            var query = ApplyFilter(
                dbContext.MailMessages.Where(m => m.TraderId == traderId),
                filter,
                sent,
                read);

            var totalCount = await query.CountAsync();

            var mails = await query
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Select(ToMailInfo)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Result<PagedResult<MailInfo>>.Ok(
                new PagedResult<MailInfo>(mails, page, pageSize, totalCount));
        }, logger, nameof(QueryService));
    }

    public async Task<Result<MailInfo>> GetUserMailAsync(long traderId, long mailId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var mail = await dbContext.MailMessages
                .Where(m => m.Id == mailId && m.TraderId == traderId)
                .Select(ToMailInfo)
                .FirstOrDefaultAsync();

            if (mail is null)
                return Result<MailInfo>.Fail("Письмо не найдено");

            return Result<MailInfo>.Ok(mail);
        }, logger, nameof(QueryService));
    }

    public async Task<Result<MailCounters>> GetMailCountersAsync(long traderId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var (sent, read) = StatusValues();
            var traderMails = dbContext.MailMessages.Where(m => m.TraderId == traderId);

            var totalCount = await traderMails.CountAsync();
            var unreadCount = await ApplyFilter(traderMails, MailFilter.Unread, sent, read).CountAsync();
            var rewardCount = await ApplyFilter(traderMails, MailFilter.Reward, sent, read).CountAsync();

            return Result<MailCounters>.Ok(new MailCounters(unreadCount, rewardCount, totalCount));
        }, logger, nameof(QueryService));
    }

    private static IQueryable<MailMessage> ApplyFilter(
        IQueryable<MailMessage> query,
        MailFilter filter,
        string sent,
        string read)
    {
        return filter switch
        {
            MailFilter.Unread => query.Where(m => m.Status == sent),
            MailFilter.Reward => query.Where(m => (m.Status == sent || m.Status == read)
                && !string.IsNullOrEmpty(m.SymbolForReward)
                && m.AmountForReward > 0),
            _ => query
        };
    }

    private static (string Sent, string Read) StatusValues()
        => (MailMessageStatus.Sent.ToString(), MailMessageStatus.Read.ToString());
}