using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Token;

public class TokenMediaUpdateServiceTest
{
    [Theory]
    [InlineData("", "icon.png", "image.png", false, false)]
    [InlineData("ZZZ", "", "image.png", true, false)]
    [InlineData("ZZZ", "icon.png", "", true, false)]
    [InlineData("NONEXISTENT", "icon.png", "image.png", false, false)]
    public async Task UpdateTokenMediaAsync_InvalidInput_ReturnsFail(string symbol, string iconUrl, string imageUrl, bool createToken, bool expectSuccess)
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        if (createToken) await HelpMethods.CreateToken(db, symbol);
        var service = new TokenMediaUpdateService(db, NullLogger<TokenMediaUpdateService>.Instance);
        var result = await service.UpdateTokenMediaAsync(symbol, iconUrl, imageUrl);
        Assert.Equal(expectSuccess, result.IsSuccess);
    }

    [Fact]
    public async Task UpdateTokenMediaAsync_ValidData_ReturnsSuccess()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new TokenMediaUpdateService(db, NullLogger<TokenMediaUpdateService>.Instance);

        var result = await service.UpdateTokenMediaAsync("ZZZ", "newicon.png", "newimage.png");

        Assert.True(result.IsSuccess);

        var token = await db.CharacterTokens.FindAsync("ZZZ");
        Assert.Equal("newicon.png", token!.IconUrl);
        Assert.Equal("newimage.png", token.ImageUrl);
    }
}
