using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Tests.HelpTools;
using Microsoft.CodeAnalysis;

namespace ArkWallet.Tests.ServiceTests;

public class TokenCreationServiceTest
{
    [Theory]
    [InlineData("", "Тест-валюта", CharacterRarity.FourStar, 10000, 1000, true, "Идентификатор токена не может быть пустым")]
    [InlineData("ZZZ", "Тест-валюта", CharacterRarity.FourStar, 10000, 0, true, "Цена должна быть больше 0")]
    [InlineData("ZZZ", "Тест-валюта", CharacterRarity.FourStar, 0, 1000, true, "Общее количество должно быть больше 0")]
    [InlineData("ZZZ", "", CharacterRarity.FourStar, 10000, 1000, true, "Имя токена не может быть пустым")]
    public async Task CreateTokenAsync_WithInvalidData_ReturnsFail(string symbol, string name, CharacterRarity rarity, int initialPrice, int maxSupply, bool isTradable, string errorMessage)
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var result = await HelpMethods.CreateToken(db, symbol, name, rarity, maxSupply, initialPrice, isTradable);

        Assert.False(result.IsSuccess);
        Assert.Equal(errorMessage, result.Message);
    }

    [Fact]
    public async Task CreateTokenAsync_WithoutData_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var service = new TokenCreationService(db);
        var result = await service.CreateTokenAsync(null);

        Assert.False(result.IsSuccess);
        Assert.Equal("Команда на создание некорректна", result.Message);
    }

    [Fact]
    public async Task CreateTokenAsync_AlreadyExist_ReturnsSuccessFirstFailSecond()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var result1 = await HelpMethods.CreateToken(db, "ZZZ");
        var result2 = await HelpMethods.CreateToken(db, "ZZZ");

        Assert.True(result1.IsSuccess);
        Assert.False(result2.IsSuccess);
        Assert.Equal("Такой токен уже существует", result2.Message);
    }
}