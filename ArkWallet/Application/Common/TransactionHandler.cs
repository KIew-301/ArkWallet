using System.Diagnostics.CodeAnalysis;
using ArkWallet.Infrastructure.Data;

namespace ArkWallet.Application.Common;

[ExcludeFromCodeCoverage(Justification = "Инфраструктурный обработчик транзакций, не содержит бизнес-логики")]
internal static class TransactionHandler
{
    internal static async Task<Result<T>> ExecuteAsync<T>(
        ArkWalletDbContext dbContext,
        Func<Task<Result<T>>> action)
    {
        if (dbContext.Database.CurrentTransaction is not null)
            return await action();

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var result = await action();

            if (result.IsSuccess)
                await transaction.CommitAsync();
            else
                await transaction.RollbackAsync();

            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    internal static async Task<Result> ExecuteAsync(
        ArkWalletDbContext dbContext,
        Func<Task<Result>> action)
    {
        if (dbContext.Database.CurrentTransaction is not null)
            return await action();

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var result = await action();

            if (result.IsSuccess)
                await transaction.CommitAsync();
            else
                await transaction.RollbackAsync();

            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
