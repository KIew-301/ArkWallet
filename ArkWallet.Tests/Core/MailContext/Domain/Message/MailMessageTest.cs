using ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Core.TradingContext.Domain.TraderAggregate;
using ArkWallet.Core.TradingContext.Domain.TokenAggregate;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Core.TradingContext.Domain.TradeAggregate;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Core.PortfolioContext.Domain.Position;
using ArkWallet.Core.GiftContext.Domain.User;
using ArkWallet.Core.MailContext.Domain.Message;
using ArkWallet.Core.GlobalGoalContext.Domain.GlobalGoal;
using ArkWallet.Core.MiningContext.Domain.Machine;
using ArkWallet.Core.MiningContext.Domain.GlobalRule;
using ArkWallet.Core.MiningContext.Domain.Engines;
using ArkWallet.Infrastructure.Data;

namespace ArkWallet.Tests.Core.MailContext.Domain.Message;

public class MailMessageTest
{
    [Fact]
    public void Create_SetsAllFieldsAndDefaults()
    {
        var mail = MailMessage.Create(new MailMessageDraft(2002, "T", "M", "Sender", 5001, "ZZZ", 5m, new DateTime(2026, 1, 1)));

        Assert.Equal(2002, mail.TraderId);
        Assert.Equal("T", mail.Title);
        Assert.Equal("M", mail.Message);
        Assert.Equal("Sender", mail.SenderName);
        Assert.Equal(5001, mail.SenderId);
        Assert.Equal("ZZZ", mail.SymbolForReward);
        Assert.Equal(5m, mail.AmountForReward);
        Assert.Equal(MailType.Notification.ToString(), mail.Type);
        Assert.Equal(MailMessageStatus.Sent.ToString(), mail.Status);
        Assert.Equal(new DateTime(2026, 1, 1), mail.CreatedAt);
        Assert.Null(mail.ReadAt);
        Assert.Null(mail.AcceptedAt);
    }

    [Fact]
    public void Create_WithType_SetsType()
    {
        var mail = MailMessage.Create(new MailMessageDraft(2002, "T", "M", "Sender", null, "", 0, new DateTime(2026, 1, 1), MailType.Reward));

        Assert.Equal(MailType.Reward.ToString(), mail.Type);
    }

    [Fact]
    public void MarkAsRead_SentMail_SetsRead()
    {
        var mail = MailMessage.Create(new MailMessageDraft(2002, "T", "M", "Sender", null, "", 0, new DateTime(2026, 1, 1)));
        var when = new DateTime(2026, 1, 2);

        mail.MarkAsRead(when);

        Assert.Equal(MailMessageStatus.Read.ToString(), mail.Status);
        Assert.Equal(when, mail.ReadAt);
    }

    [Fact]
    public void MarkAsRead_AlreadyAccepted_DoesNotChange()
    {
        var mail = MailMessage.Create(new MailMessageDraft(2002, "T", "M", "Sender", null, "", 0, new DateTime(2026, 1, 1)));
        mail.MarkAsAccepted(new DateTime(2026, 1, 2));

        mail.MarkAsRead(new DateTime(2026, 1, 3));

        Assert.Equal(MailMessageStatus.Accepted.ToString(), mail.Status);
        Assert.Null(mail.ReadAt);
    }

    [Fact]
    public void MarkAsAccepted_FromSent_SetsAccepted()
    {
        var mail = MailMessage.Create(new MailMessageDraft(2002, "T", "M", "Sender", null, "", 0, new DateTime(2026, 1, 1)));
        var when = new DateTime(2026, 1, 2);

        mail.MarkAsAccepted(when);

        Assert.Equal(MailMessageStatus.Accepted.ToString(), mail.Status);
        Assert.Equal(when, mail.AcceptedAt);
    }

    [Fact]
    public void MarkAsAccepted_FromRead_SetsAccepted()
    {
        var mail = MailMessage.Create(new MailMessageDraft(2002, "T", "M", "Sender", null, "", 0, new DateTime(2026, 1, 1)));
        mail.MarkAsRead(new DateTime(2026, 1, 2));

        mail.MarkAsAccepted(new DateTime(2026, 1, 3));

        Assert.Equal(MailMessageStatus.Accepted.ToString(), mail.Status);
        Assert.Equal(new DateTime(2026, 1, 3), mail.AcceptedAt);
    }
}
