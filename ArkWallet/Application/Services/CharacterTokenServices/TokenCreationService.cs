using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.CharacterTokenServices
{
    internal class TokenCreationService(ArkWalletDbContext dbContext) : ITokenCreationService
    {
        public async Task<TokenCreationResult> CreateTokenAsync(CreateTokenCommand command)
        {
            if (command == null)
                return new TokenCreationResult(false, null, "Команда на создание некорректна");
            if (command.StartPrice <= 0)
                return new TokenCreationResult(false, null, "Цена должна быть больше 0");
            if (command.TotalSupply <= 0)
                return new TokenCreationResult(false, null, "Общее количество должно быть больше 0");
            if (command.Symbol == "")
                return new TokenCreationResult(false, null, "Идентификатор токена не может быть пустым");
            if (command.Name == "")
                return new TokenCreationResult(false, null, "Имя токена не может быть пустым");

            var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == command.Symbol);

            if (token != null)
                return new TokenCreationResult(false, null, "Такой токен уже существует");

            token = command.ToEntity();
            await dbContext.CharacterTokens.AddAsync(token);
            await dbContext.SaveChangesAsync();

            return new TokenCreationResult(true, TokenInfoDto.FromEntity(token));
        }
    }
}
