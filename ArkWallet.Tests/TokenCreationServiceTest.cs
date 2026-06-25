using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Tests;

public class TokenCreationServiceTest
{
    [Theory]
    [InlineData("", "Тест-валюта", CharacterRarity.FourStar, 1000, 10000, true, "Идентификатор токена не может быть пустым")]
    public async Task CreateTokenAsync_WithInvalidData_ReturnsFailure(string symbol, string name, CharacterRarity rarity, int initialPrice, int maxSupply, bool isTradable, string errorMessage)
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var result = await HelpMethods.CreateToken(db, symbol, name, rarity, maxSupply, initialPrice, isTradable);

        Assert.False(result.IsSuccess);
        Assert.Equal(errorMessage, result.Message);
    }
}