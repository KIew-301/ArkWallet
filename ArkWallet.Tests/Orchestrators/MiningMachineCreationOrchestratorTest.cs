using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Application.Contracts.Orchestrators;
using ArkWallet.Application.Services.Orchestrators;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.Orchestrators;

public class MiningMachineCreationOrchestratorTest
{
    private static MiningMachineCreationCommand BuildCommand(bool withRules = true) =>
        new(
            "SMAI",
            10,
            80,
            true,
            "img.zzz",
            1m,
            withRules
                ? [new MiningMachineRuleCreationCommand(0, "AAA", 1.5m)]
                : null);

    private static MiningMachineCreationOrchestrator CreateOrchestrator(
        ArkWalletDbContext db,
        Mock<IMiningMachineCreationService> machineService,
        Mock<IMiningMachineRuleCreationService> ruleService) =>
        new(
            db,
            machineService.Object,
            ruleService.Object,
            NullLogger<MiningMachineCreationOrchestrator>.Instance);

    [Fact]
    public async Task CreateMachineAsync_WithRules_CreatesMachineAndRules()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var machineService = new Mock<IMiningMachineCreationService>();
        machineService
            .Setup(x => x.CreateMachineAsync(It.IsAny<MiningMachineCreationCommand>()))
            .ReturnsAsync(Result<MiningMachineCreationData>.Ok(new MiningMachineCreationData(5, "SM-01")));

        var ruleService = new Mock<IMiningMachineRuleCreationService>();
        ruleService
            .Setup(x => x.CreateRulesAsync(It.IsAny<IEnumerable<MiningMachineRuleCreationCommand>>()))
            .ReturnsAsync(Result<List<long>>.Ok([1]));

        var orchestrator = CreateOrchestrator(db, machineService, ruleService);

        var result = await orchestrator.CreateMachineAsync(BuildCommand());

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(5, data.Id);

        machineService.Verify(x => x.CreateMachineAsync(It.IsAny<MiningMachineCreationCommand>()), Times.Once);
        ruleService.Verify(
            x => x.CreateRulesAsync(It.Is<IEnumerable<MiningMachineRuleCreationCommand>>(
                commands => commands.Count() == 1 && commands.First().MiningMachineId == 5)),
            Times.Once);
    }

    [Fact]
    public async Task CreateMachineAsync_WithoutRules_SkipsRulesCreation()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var machineService = new Mock<IMiningMachineCreationService>();
        machineService
            .Setup(x => x.CreateMachineAsync(It.IsAny<MiningMachineCreationCommand>()))
            .ReturnsAsync(Result<MiningMachineCreationData>.Ok(new MiningMachineCreationData(5, "SM-01")));

        var ruleService = new Mock<IMiningMachineRuleCreationService>();

        var orchestrator = CreateOrchestrator(db, machineService, ruleService);

        var result = await orchestrator.CreateMachineAsync(BuildCommand(withRules: false));

        Assert.True(result.IsSuccess, result.Message);
        ruleService.Verify(x => x.CreateRulesAsync(It.IsAny<IEnumerable<MiningMachineRuleCreationCommand>>()), Times.Never);
    }

    [Fact]
    public async Task CreateMachineAsync_MachineCreationFails_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var machineService = new Mock<IMiningMachineCreationService>();
        machineService
            .Setup(x => x.CreateMachineAsync(It.IsAny<MiningMachineCreationCommand>()))
            .ReturnsAsync(Result<MiningMachineCreationData>.Fail("Некорректные данные"));

        var ruleService = new Mock<IMiningMachineRuleCreationService>();

        var orchestrator = CreateOrchestrator(db, machineService, ruleService);

        var result = await orchestrator.CreateMachineAsync(BuildCommand());

        Assert.False(result.IsSuccess);
        Assert.Equal("Некорректные данные", result.Message);
        ruleService.Verify(x => x.CreateRulesAsync(It.IsAny<IEnumerable<MiningMachineRuleCreationCommand>>()), Times.Never);
    }

    [Fact]
    public async Task CreateMachineAsync_RuleCreationFails_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var machineService = new Mock<IMiningMachineCreationService>();
        machineService
            .Setup(x => x.CreateMachineAsync(It.IsAny<MiningMachineCreationCommand>()))
            .ReturnsAsync(Result<MiningMachineCreationData>.Ok(new MiningMachineCreationData(5, "SM-01")));

        var ruleService = new Mock<IMiningMachineRuleCreationService>();
        ruleService
            .Setup(x => x.CreateRulesAsync(It.IsAny<IEnumerable<MiningMachineRuleCreationCommand>>()))
            .ReturnsAsync(Result<List<long>>.Fail("Правило уже существует"));

        var orchestrator = CreateOrchestrator(db, machineService, ruleService);

        var result = await orchestrator.CreateMachineAsync(BuildCommand());

        Assert.False(result.IsSuccess);
        Assert.Equal("Правило уже существует", result.Message);
    }

    [Fact]
    public async Task CreateMachinesAsync_MultipleMachines_PairsRulesWithEachMachine()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var commands = new[]
        {
            BuildCommand(),
            BuildCommand()
        };

        var machineService = new Mock<IMiningMachineCreationService>();
        machineService
            .Setup(x => x.CreateMachinesAsync(It.IsAny<IEnumerable<MiningMachineCreationCommand>>()))
            .ReturnsAsync(Result<List<MiningMachineCreationData>>.Ok(
                new List<MiningMachineCreationData> { new(1, "SM-01"), new(2, "SM-02") }));

        var ruleService = new Mock<IMiningMachineRuleCreationService>();
        ruleService
            .Setup(x => x.CreateRulesAsync(It.IsAny<IEnumerable<MiningMachineRuleCreationCommand>>()))
            .ReturnsAsync(Result<List<long>>.Ok([1, 2]));

        var orchestrator = CreateOrchestrator(db, machineService, ruleService);

        var result = await orchestrator.CreateMachinesAsync(commands);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var machines));
        Assert.Equal(2, machines.Count);

        ruleService.Verify(
            x => x.CreateRulesAsync(It.Is<IEnumerable<MiningMachineRuleCreationCommand>>(commands =>
                commands.Count() == 2 &&
                commands.First().MiningMachineId == 1 &&
                commands.Last().MiningMachineId == 2)),
            Times.Once);
    }

    [Fact]
    public async Task CreateMachinesAsync_EmptyList_ReturnsEmptySuccess()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var machineService = new Mock<IMiningMachineCreationService>();
        var ruleService = new Mock<IMiningMachineRuleCreationService>();

        var orchestrator = CreateOrchestrator(db, machineService, ruleService);

        var result = await orchestrator.CreateMachinesAsync([]);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var machines));
        Assert.Empty(machines);
        machineService.Verify(x => x.CreateMachinesAsync(It.IsAny<IEnumerable<MiningMachineCreationCommand>>()), Times.Never);
    }
}
