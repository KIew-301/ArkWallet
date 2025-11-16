using ArkWallet.Application.Contracts;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Dtos;

namespace ArkWallet.Application.Services.CharacterTokenServices
{
    internal class TokenCreationService : ITokenCreationService
    {
        readonly IUnitOfWork _unitOfWork;

        public TokenCreationService(
            IUnitOfWork unitOfWork
            )
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<TokenCreationResult> CreateTokenAsync(CreateTokenCommand command)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                if (command == null)
                    return new TokenCreationResult(false, null, "Комманда на создание некорректна.");
                if (command.StartPrice <= 0)
                    return new TokenCreationResult(false, null, "Цена должна быть больше 0.");
                if (command.TotalSupply <= 0)
                    return new TokenCreationResult(false, null, "Общее количество должно быть больше 0.");

                var token = await _unitOfWork.Tokens.GetBySymbolAsync(command.Symbol);

                if (token != null)
                    return new TokenCreationResult(false, null, "Такой токен уже существует.");

                token = command.ToEntity();
                await _unitOfWork.Tokens.AddAsync(token);

                return new TokenCreationResult(true, TokenInfoDto.FromEntity(token));
            });
        }
    }
}
