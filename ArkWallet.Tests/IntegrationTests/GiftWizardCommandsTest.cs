using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.GiftServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Wizard;
using Moq;

namespace ArkWallet.Tests.IntegrationTests;

public class GiftWizardCommandsTest : IDisposable
{
    private readonly ServiceMocks _m;
    private readonly WizardEngine _engine;

    private const long UserId = 1001;

    public GiftWizardCommandsTest()
    {
        _m = WizardEngineTestHelper.Build();
        _engine = _m.Engine;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private void SetupGiftSendSuccess(long recipientId = 1002)
    {
        _m.GiftSending
            .Setup(s => s.SendGiftAsync(UserId, recipientId))
            .ReturnsAsync(Result<GiftSendResult>.Ok(
                new GiftSendResult(Guid.NewGuid(), UserId, recipientId, "ZZZ", 1, 100m)));
    }

    // ═══════════════════════════════════════════════════════════
    //  /send_gift in group — requires reply_to_message
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task SendGift_Group_WithReply_SendsDirectly()
    {
        SetupGiftSendSuccess(2001);

        var result = await _engine.ProcessInput(UserId, "/send_gift", ChatType.Group, replyToUserId: 2001);

        Assert.NotNull(result.Message);
        Assert.Contains("Подарок отправлен", result.Message);
        Assert.Contains("ZZZ", result.Message);
        _m.GiftSending.Verify(s => s.SendGiftAsync(UserId, 2001), Times.Once);
    }

    [Fact]
    public async Task SendGift_Group_WithoutReply_ReturnsError()
    {
        var result = await _engine.ProcessInput(UserId, "/send_gift", ChatType.Group, replyToUserId: null);

        Assert.NotNull(result.Message);
        Assert.Contains("Ответьте на сообщение", result.Message);
    }

    [Fact]
    public async Task SendGift_Group_ServiceFails_ReturnsError()
    {
        _m.GiftSending
            .Setup(s => s.SendGiftAsync(UserId, 2001))
            .ReturnsAsync(Result<GiftSendResult>.Fail("Нет подходящих токенов в портфеле"));

        var result = await _engine.ProcessInput(UserId, "/send_gift", ChatType.Group, replyToUserId: 2001);

        Assert.NotNull(result.Message);
        Assert.Contains("подходящих токенов", result.Message);
    }

    [Fact]
    public async Task SendGift_Group_DomainException_ShownToUser()
    {
        _m.GiftSending
            .Setup(s => s.SendGiftAsync(UserId, 2001))
            .ReturnsAsync(Result<GiftSendResult>.Fail("Нельзя отправлять более 1 токена одному человеку раз в 8 часов"));

        var result = await _engine.ProcessInput(UserId, "/send_gift", ChatType.Group, replyToUserId: 2001);

        Assert.NotNull(result.Message);
        Assert.Contains("8 часов", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  /send_gift in private — shows user list
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task SendGift_Private_ShowsUserList()
    {
        _m.TraderQuery
            .Setup(s => s.GetAllTradersWithoutBotsAsync())
            .ReturnsAsync(Result<List<(string Username, long TelegramId)>>.Ok(
                new List<(string, long)> { ("alice", 2001), ("bob", 2002) }));

        var result = await _engine.ProcessInput(UserId, "/send_gift", ChatType.Private);

        Assert.NotNull(result.Message);
        Assert.Contains("получателя", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Buttons);
        Assert.Equal(2, result.Buttons.Count);
        Assert.Contains("alice", result.Buttons[0].Text);
        Assert.Equal("gift_send 2001", result.Buttons[0].Value);
    }

    [Fact]
    public async Task SendGift_Private_ExcludesSelfFromList()
    {
        _m.TraderQuery
            .Setup(s => s.GetAllTradersWithoutBotsAsync())
            .ReturnsAsync(Result<List<(string Username, long TelegramId)>>.Ok(
                new List<(string, long)> { ("alice", 2001), ("self", UserId) }));

        var result = await _engine.ProcessInput(UserId, "/send_gift", ChatType.Private);

        Assert.NotNull(result.Buttons);
        Assert.Single(result.Buttons);
        Assert.Equal("gift_send 2001", result.Buttons[0].Value);
    }

    [Fact]
    public async Task SendGift_Private_NoOtherUsers_ReturnsMessage()
    {
        _m.TraderQuery
            .Setup(s => s.GetAllTradersWithoutBotsAsync())
            .ReturnsAsync(Result<List<(string Username, long TelegramId)>>.Ok(
                new List<(string, long)> { ("self", UserId) }));

        var result = await _engine.ProcessInput(UserId, "/send_gift", ChatType.Private);

        Assert.NotNull(result.Message);
        Assert.Contains("Нет других пользователей", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  gift_send callback — from button click
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GiftSend_Callback_SendsGift()
    {
        SetupGiftSendSuccess(2001);

        var result = await _engine.ProcessInput(UserId, "gift_send 2001");

        Assert.NotNull(result.Message);
        Assert.Contains("Подарок отправлен", result.Message);
        _m.GiftSending.Verify(s => s.SendGiftAsync(UserId, 2001), Times.Once);
    }

    [Fact]
    public async Task GiftSend_Callback_InvalidId_ReturnsError()
    {
        var result = await _engine.ProcessInput(UserId, "gift_send abc");

        Assert.NotNull(result.Message);
        Assert.Contains("Неверный ID", result.Message);
    }

    [Fact]
    public async Task GiftSend_Callback_ServiceFails_ReturnsError()
    {
        _m.GiftSending
            .Setup(s => s.SendGiftAsync(UserId, 2001))
            .ReturnsAsync(Result<GiftSendResult>.Fail("Получатель не найден"));

        var result = await _engine.ProcessInput(UserId, "gift_send 2001");

        Assert.NotNull(result.Message);
        Assert.Contains("Получатель не найден", result.Message);
    }

    [Fact]
    public async Task GiftSend_Callback_SuccessMessageContainsPrice()
    {
        _m.GiftSending
            .Setup(s => s.SendGiftAsync(UserId, 2001))
            .ReturnsAsync(Result<GiftSendResult>.Ok(
                new GiftSendResult(Guid.NewGuid(), UserId, 2001, "ZZZ", 1, 100m)));

        var result = await _engine.ProcessInput(UserId, "gift_send 2001");

        Assert.Contains("100", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  /get_gifts_list
    // ═══════════════════════════════════════════════════════════

    private static readonly Guid GiftId1 = Guid.NewGuid();
    private static readonly Guid GiftId2 = Guid.NewGuid();

    private void SetupPendingGifts(params GiftInfo[] gifts)
        => _m.GiftQuery
            .Setup(s => s.GetPendingGiftsAsync(UserId))
            .ReturnsAsync(Result<List<GiftInfo>>.Ok(gifts.ToList()));

    [Fact]
    public async Task GetGiftsList_Private_ShowsGiftsWithButtons()
    {
        SetupPendingGifts(
            new GiftInfo(GiftId1, 2001, "ZZZ", 2, DateTime.UtcNow),
            new GiftInfo(GiftId2, 2002, "ARK_001", 5, DateTime.UtcNow));

        var result = await _engine.ProcessInput(UserId, "/get_gifts_list", ChatType.Private);

        Assert.NotNull(result.Message);
        Assert.Contains("ZZZ", result.Message);
        Assert.Contains("ARK_001", result.Message);
        Assert.NotNull(result.Buttons);
        Assert.Equal(4, result.Buttons.Count);
        Assert.Equal($"collect_gift {GiftId1}", result.Buttons[0].Value);
        Assert.Equal($"collect_gift {GiftId2}", result.Buttons[1].Value);
        Assert.Equal("/collect_all_gifts", result.Buttons[2].Value);
        Assert.Equal("/get_gifts_list", result.Buttons[3].Value);
    }

    [Fact]
    public async Task GetGiftsList_NoGifts_ReturnsMessage()
    {
        SetupPendingGifts();

        var result = await _engine.ProcessInput(UserId, "/get_gifts_list", ChatType.Private);

        Assert.NotNull(result.Message);
        Assert.Contains("нет подарков", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.Buttons);
    }

    [Fact]
    public async Task GetGiftsList_Group_WorksWithoutButtons()
    {
        SetupPendingGifts(new GiftInfo(GiftId1, 2001, "ZZZ", 2, DateTime.UtcNow));

        var result = await _engine.ProcessInput(UserId, "/get_gifts_list", ChatType.Group);

        Assert.NotNull(result.Message);
        Assert.Contains("ZZZ", result.Message);
        Assert.Null(result.Buttons);
    }

    [Fact]
    public async Task GetGiftsList_QueryServiceFails_ReturnsError()
    {
        _m.GiftQuery
            .Setup(s => s.GetPendingGiftsAsync(UserId))
            .ReturnsAsync(Result<List<GiftInfo>>.Fail("Ошибка на стороне сервера"));

        var result = await _engine.ProcessInput(UserId, "/get_gifts_list", ChatType.Private);

        Assert.NotNull(result.Message);
        Assert.Contains("Ошибка", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  /collect_all_gifts
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task CollectAllGifts_Private_CollectsAll()
    {
        _m.GiftReceiving
            .Setup(s => s.ReceiveAllGiftsAsync(UserId))
            .ReturnsAsync(Result<GiftReceiveAllResult>.Ok(
                new GiftReceiveAllResult(UserId, 3, new List<GiftReceiveResult>())));

        var result = await _engine.ProcessInput(UserId, "/collect_all_gifts", ChatType.Private);

        Assert.NotNull(result.Message);
        Assert.Contains("3", result.Message);
        _m.GiftReceiving.Verify(s => s.ReceiveAllGiftsAsync(UserId), Times.Once);
    }

    [Fact]
    public async Task CollectAllGifts_Group_Works()
    {
        _m.GiftReceiving
            .Setup(s => s.ReceiveAllGiftsAsync(UserId))
            .ReturnsAsync(Result<GiftReceiveAllResult>.Ok(
                new GiftReceiveAllResult(UserId, 1, new List<GiftReceiveResult>())));

        var result = await _engine.ProcessInput(UserId, "/collect_all_gifts", ChatType.Group);

        Assert.NotNull(result.Message);
        Assert.Contains("Собрано подарков", result.Message);
        Assert.Null(result.Buttons);
    }

    [Fact]
    public async Task CollectAllGifts_ServiceFails_ReturnsError()
    {
        _m.GiftReceiving
            .Setup(s => s.ReceiveAllGiftsAsync(UserId))
            .ReturnsAsync(Result<GiftReceiveAllResult>.Fail("Нет подарков для получения"));

        var result = await _engine.ProcessInput(UserId, "/collect_all_gifts", ChatType.Private);

        Assert.NotNull(result.Message);
        Assert.Contains("Нет подарков", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  collect_gift callback (private button)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task CollectGift_Callback_CollectsGift()
    {
        _m.GiftReceiving
            .Setup(s => s.ReceiveGiftAsync(UserId, GiftId1))
            .ReturnsAsync(Result<GiftReceiveResult>.Ok(
                new GiftReceiveResult(GiftId1, 2001, UserId, "ZZZ", 2)));

        var result = await _engine.ProcessInput(UserId, $"collect_gift {GiftId1}", ChatType.Private);

        Assert.NotNull(result.Message);
        Assert.Contains("Подарок собран", result.Message);
        Assert.Contains("ZZZ", result.Message);
        _m.GiftReceiving.Verify(s => s.ReceiveGiftAsync(UserId, GiftId1), Times.Once);
    }

    [Fact]
    public async Task CollectGift_Callback_InvalidId_ReturnsError()
    {
        var result = await _engine.ProcessInput(UserId, "collect_gift abc", ChatType.Private);

        Assert.NotNull(result.Message);
        Assert.Contains("Неверный ID", result.Message);
        _m.GiftReceiving.Verify(s => s.ReceiveGiftAsync(It.IsAny<long>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task CollectGift_Callback_ServiceFails_ReturnsError()
    {
        _m.GiftReceiving
            .Setup(s => s.ReceiveGiftAsync(UserId, GiftId1))
            .ReturnsAsync(Result<GiftReceiveResult>.Fail("Подарок уже принят"));

        var result = await _engine.ProcessInput(UserId, $"collect_gift {GiftId1}", ChatType.Private);

        Assert.NotNull(result.Message);
        Assert.Contains("Подарок уже принят", result.Message);
    }

    [Fact]
    public async Task CollectGift_Group_Blocked()
    {
        var result = await _engine.ProcessInput(UserId, $"collect_gift {GiftId1}", ChatType.Group);

        Assert.Equal("", result.Message);
        Assert.Null(result.Buttons);
        _m.GiftReceiving.Verify(s => s.ReceiveGiftAsync(It.IsAny<long>(), It.IsAny<Guid>()), Times.Never);
    }
}
