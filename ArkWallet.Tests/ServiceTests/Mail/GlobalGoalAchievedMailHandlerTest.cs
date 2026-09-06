using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MailServices;
using ArkWallet.Application.Services.MailServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.GlobalGoalContext;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.ServiceTests.Mail;

public class GlobalGoalAchievedMailHandlerTest
{
    [Fact]
    public async Task Handle_CreatesRewardMailsForAllNonBotTraders()
    {
        using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2002);
        await HelpMethods.RegisterTrader(db, 3003);
        await HelpMethods.RegisterTrader(db, 101);

        List<MailCreateCommand>? captured = null;
        var mailService = new Mock<IMailMessageService>();
        mailService
            .Setup(m => m.CreateManyAsync(It.IsAny<IReadOnlyList<MailCreateCommand>>()))
            .ReturnsAsync(Result<List<MailCreateResult>>.Ok(new()))
            .Callback<IReadOnlyList<MailCreateCommand>>(c => captured = c.ToList());

        var handler = new GlobalGoalAchievedMailHandler(db, mailService.Object);
        await handler.Handle(new GlobalGoalAchievedEvent(
            GoalName: "Goal1", AchievedAt: DateTime.UtcNow, Target: 10000m,
            SymbolForReward: "ZZZ", AmountForReward: 10m), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(2, captured!.Count);
        Assert.DoesNotContain(captured, c => c.TraderId == 101);
        foreach (var c in captured)
        {
            Assert.Equal(MailType.Reward.ToString(), c.Type);
            Assert.Equal("ZZZ", c.SymbolForReward);
            Assert.Equal(10m, c.AmountForReward);
            Assert.Null(c.SenderId);
            Assert.Contains("Goal1", c.Title);
            Assert.Contains("Goal1", c.Message);
        }
    }

    [Fact]
    public async Task Handle_NoNonBotTraders_DoesNotCallCreate()
    {
        using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 101);

        var mailService = new Mock<IMailMessageService>();
        mailService
            .Setup(m => m.CreateManyAsync(It.IsAny<IReadOnlyList<MailCreateCommand>>()))
            .ReturnsAsync(Result<List<MailCreateResult>>.Ok(new()));

        var handler = new GlobalGoalAchievedMailHandler(db, mailService.Object);
        await handler.Handle(new GlobalGoalAchievedEvent(
            "Goal1", DateTime.UtcNow, 10000m, "ZZZ", 10m), CancellationToken.None);

        mailService.Verify(m => m.CreateManyAsync(It.IsAny<IReadOnlyList<MailCreateCommand>>()), Times.Once);
    }
}
