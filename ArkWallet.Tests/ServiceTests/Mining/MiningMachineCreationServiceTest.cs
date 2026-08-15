using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Application.Services.MiningMachineServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Mining;

public class MiningMachineCreationServiceTest
{
    private static MiningMachineCreationService CreateService(ArkWalletDbContext db) =>
        new(db, NullLogger<MiningMachineCreationService>.Instance);

    private static MiningMachineCreationCommand BuildCommand(
        string type = "SMAI",
        int switchingTime = 10,
        decimal reusability = 80,
        bool isActiveForSale = true,
        string image = "img.zzz",
        decimal efficiency = 1,
        List<MiningMachineRuleCreationCommand>? rules = null) =>
        new(type, switchingTime, reusability, isActiveForSale, image, efficiency, rules);

    [Fact]
    public async Task CreateMachineAsync_AllFields_ReturnsSuccess()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).CreateMachineAsync(BuildCommand());

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.True(data.Id > 0);
        var machine = await db.MiningMachines.FindAsync(data.Id);
        Assert.NotNull(machine);
        Assert.Equal(machine!.Name, data.Name);
    }

    [Fact]
    public async Task CreateMachineAsync_NullCommand_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).CreateMachineAsync(null!);

        Assert.False(result.IsSuccess);
        Assert.Contains("некорректна", result.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CreateMachineAsync_InvalidSwitchingTime_ReturnsFail(int switchingTime)
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).CreateMachineAsync(BuildCommand(switchingTime: switchingTime));

        Assert.False(result.IsSuccess);
        Assert.Contains("Время переключения", result.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task CreateMachineAsync_InvalidReusability_ReturnsFail(decimal reusability)
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).CreateMachineAsync(BuildCommand(reusability: reusability));

        Assert.False(result.IsSuccess);
        Assert.Contains("Переиспользуемость", result.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task CreateMachineAsync_InvalidEfficiency_ReturnsFail(decimal efficiency)
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).CreateMachineAsync(BuildCommand(efficiency: efficiency));

        Assert.False(result.IsSuccess);
        Assert.Contains("Коэффициент производительности", result.Message);
    }

    [Fact]
    public async Task CreateMachineAsync_DuplicateName_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var first = await CreateService(db).CreateMachineAsync(BuildCommand());
        Assert.True(first.IsSuccess, first.Message);

        var duplicate = await CreateService(db).CreateMachineAsync(BuildCommand());

        Assert.False(duplicate.IsSuccess);
        Assert.Contains("уже существует", duplicate.Message);
        Assert.Equal(1, await db.MiningMachines.CountAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateMachineAsync_EmptyImage_ReturnsFail(string image)
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).CreateMachineAsync(BuildCommand(image: image));

        Assert.False(result.IsSuccess);
        Assert.Contains("изображение", result.Message);
    }

    [Fact]
    public async Task CreateMachineAsync_UnknownType_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).CreateMachineAsync(BuildCommand(type: "UNKNOWN"));

        Assert.False(result.IsSuccess);
        Assert.Contains("Неизвестный тип", result.Message);
    }

    [Fact]
    public async Task CreateMachineAsync_TypeIsCaseInsensitive_ReturnsSuccess()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).CreateMachineAsync(BuildCommand(type: "smai"));

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        var machine = await db.MiningMachines.FindAsync(data.Id);
        Assert.Equal(MiningMachineType.SMAI, machine!.Type);
    }

    [Fact]
    public async Task CreateMachinesAsync_AllFields_ReturnsSuccess()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).CreateMachinesAsync(
            new[] { BuildCommand(efficiency: 1m), BuildCommand(efficiency: 1.5m) });

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2, data.Count);
        Assert.Equal(2, await db.MiningMachines.CountAsync());
    }

    [Fact]
    public async Task CreateMachinesAsync_DuplicateNameInBatch_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).CreateMachinesAsync(
            new[] { BuildCommand(), BuildCommand() });

        Assert.False(result.IsSuccess);
        Assert.Contains("уже существуют", result.Message);
        Assert.Empty(await db.MiningMachines.ToListAsync());
    }

    [Fact]
    public async Task CreateMachinesAsync_ExistingNameInDb_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var first = await CreateService(db).CreateMachineAsync(BuildCommand());
        Assert.True(first.IsSuccess, first.Message);

        var result = await CreateService(db).CreateMachinesAsync(
            new[] { BuildCommand(), BuildCommand(efficiency: 1.5m) });

        Assert.False(result.IsSuccess);
        Assert.Contains("уже существуют", result.Message);
        Assert.Equal(1, await db.MiningMachines.CountAsync());
    }

    [Fact]
    public async Task CreateMachinesAsync_EmptyList_ReturnsEmptySuccess()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).CreateMachinesAsync([]);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Empty(data);
    }
}
