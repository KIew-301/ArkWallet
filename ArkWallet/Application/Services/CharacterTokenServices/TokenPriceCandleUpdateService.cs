using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.CharacterTokenServices
{
    internal class TokenPriceCandleUpdateService(ArkWalletDbContext dbContext)
    {
        public async Task UpdateTokenPriceCandleAsync(string symbol, decimal newPrice)
        {
            var lastCandle = await dbContext.PriceCandles
                .Where(c => c.CharacterTokenId == symbol)
                .OrderByDescending(c => c.Timestamp)
                .FirstOrDefaultAsync();

            if (lastCandle == null)
            {
                lastCandle = PriceCandle.CreateNew(symbol, newPrice, DateTime.UtcNow);
                await dbContext.PriceCandles.AddAsync(lastCandle);
            }
            else
            {
                lastCandle.Update(newPrice);
                dbContext.PriceCandles.Update(lastCandle);
            }

            await dbContext.SaveChangesAsync();
        }
    }
}
