using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.CharacterTokenServices;
using static Result<TokenCreationData>;

internal class TokenCreationService(ArkWalletDbContext dbContext, ILogger<TokenCreationService> logger) : ITokenCreationService
{
    public async Task<Result<TokenCreationData>> CreateTokenAsync(CreateTokenCommand command)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (command == null)
                return Fail("Команда на создание некорректна");
            if (command.StartPrice <= 0)
                return Fail("Цена должна быть больше 0");
            if (command.TotalSupply <= 0)
                return Fail("Общее количество должно быть больше 0");
            if (command.Symbol == "")
                return Fail("Идентификатор токена не может быть пустым");
            if (command.Name == "")
                return Fail("Имя токена не может быть пустым");

            var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == command.Symbol);

            if (token != null)
                return Fail("Такой токен уже существует");

            token = command.ToEntity();
            await dbContext.CharacterTokens.AddAsync(token);
            await dbContext.SaveChangesAsync();

            return Ok(new(TokenInfoDto.FromEntity(token)));
        }, logger, nameof(TokenCreationService));
    }
}
