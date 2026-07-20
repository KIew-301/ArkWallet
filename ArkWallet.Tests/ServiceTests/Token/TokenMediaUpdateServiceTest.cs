using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Token;

public class TokenMediaUpdateServiceTest
{
    [Fact]
    public async Task UpdateTokenMediaAsync_EmptySymbol_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var service = new TokenMediaUpdateService(db, NullLogger<TokenMediaUpdateService>.Instance);

        var result = await service.UpdateTokenMediaAsync("", "icon.png", "image.png");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateTokenMediaAsync_EmptyIconUrl_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new TokenMediaUpdateService(db, NullLogger<TokenMediaUpdateService>.Instance);

        var result = await service.UpdateTokenMediaAsync("ZZZ", "", "image.png");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateTokenMediaAsync_EmptyImageUrl_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new TokenMediaUpdateService(db, NullLogger<TokenMediaUpdateService>.Instance);

        var result = await service.UpdateTokenMediaAsync("ZZZ", "icon.png", "");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateTokenMediaAsync_TokenNotFound_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var service = new TokenMediaUpdateService(db, NullLogger<TokenMediaUpdateService>.Instance);

        var result = await service.UpdateTokenMediaAsync("NONEXISTENT", "icon.png", "image.png");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateTokenMediaAsync_ValidData_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new TokenMediaUpdateService(db, NullLogger<TokenMediaUpdateService>.Instance);

        var result = await service.UpdateTokenMediaAsync("ZZZ", "newicon.png", "newimage.png");

        Assert.True(result.IsSuccess);

        var token = await db.CharacterTokens.FindAsync("ZZZ");
        Assert.Equal("newicon.png", token!.IconUrl);
        Assert.Equal("newimage.png", token.ImageUrl);
    }
}
