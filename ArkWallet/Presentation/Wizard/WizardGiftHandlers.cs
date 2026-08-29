using ArkWallet.Application.Contracts.GiftServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Infrastructure.Wizard;

partial class WizardEngine
{
    public async Task<WizardResult> HandleQuickGift(long senderId, string recipientIdStr)
    {
        if (!long.TryParse(recipientIdStr, out var recipientId))
            return new WizardResult { Message = "Неверный ID получателя." };

        var giftResult = await _giftSendingService.SendGiftAsync(senderId, recipientId);

        if (!giftResult.IsSuccess)
            return new WizardResult { Message = giftResult.Message };

        if (!giftResult.TryGetData(out var data))
            return new WizardResult { Message = "Ошибка получения результата." };

        return new WizardResult
        {
            Message = $"🎁 Подарок отправлен!\n"
                + $"   Токен: {data.TokenSymbol}\n"
                + $"   Количество: {data.Quantity}\n"
                + $"   Стоимость: {data.Quantity * data.PriceAtSend:F2}{Descriptor.CurrencySymbol}"
        };
    }

    public async Task<WizardResult> HandleGiftListUsers(long senderId)
    {
        var tradersResult = await _traderQueryService.GetAllTradersWithoutBotsAsync();
        if (!tradersResult.TryGetData(out var traders))
            return new WizardResult { Message = "Не удалось загрузить список пользователей." };

        var otherTraders = traders.Where(t => t.TelegramId != senderId).ToList();

        if (otherTraders.Count == 0)
            return new WizardResult { Message = "Нет других пользователей для отправки подарка." };

        var buttons = otherTraders
            .Take(20)
            .Select(t => new QuickButton { Text = $"@{t.Username}", Value = $"gift_send {t.TelegramId}" })
            .ToList();

        return new WizardResult
        {
            Message = "Выберите получателя подарка:",
            Buttons = buttons
        };
    }

    public async Task<StepResult> HandleGetGiftsList(UserSession session, string input)
    {
        var result = await _giftQueryService.GetPendingGiftsAsync(session.Id);

        if (!result.TryGetData(out var gifts))
            return StepResult.Error(result.Message);

        if (gifts == null || gifts.Count == 0)
            return StepResult.Ok("completed", "🎁 У вас нет подарков, ожидающих получения.");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"🎁 Подарки в ожидании — {gifts.Count}:");
        sb.AppendLine();

        var buttons = new List<QuickButton>();

        foreach (var gift in gifts)
        {
            sb.AppendLine($"• {gift.TokenSymbol} x{gift.Quantity:F0}");
            buttons.Add(new QuickButton
            {
                Text = $"🎁 Собрать {gift.TokenSymbol} x{gift.Quantity:F0}",
                Value = $"collect_gift {gift.GiftId}"
            });
        }

        buttons.Add(new QuickButton { Text = "✅ Собрать все", Value = "/collect_all_gifts" });
        buttons.Add(new QuickButton { Text = "🔄 Обновить", Value = "/get_gifts_list" });

        var stepResult = StepResult.Ok("completed", sb.ToString());
        stepResult.Buttons = buttons;
        return stepResult;
    }

    public async Task<StepResult> HandleCollectAllGifts(UserSession session, string input)
    {
        var result = await _giftReceivingService.ReceiveAllGiftsAsync(session.Id);

        if (!result.TryGetData(out var data))
            return StepResult.Error(result.Message);

        return StepResult.Ok("completed", $"✅ Собрано подарков: {data.Count}");
    }

    public async Task<WizardResult> HandleCollectGift(long recipientId, string giftIdStr)
    {
        if (!Guid.TryParse(giftIdStr, out var giftId))
            return new WizardResult { Message = "Неверный ID подарка." };

        var result = await _giftReceivingService.ReceiveGiftAsync(recipientId, giftId);

        if (!result.TryGetData(out var data))
            return new WizardResult { Message = result.Message };

        return new WizardResult
        {
            Message = $"🎁 Подарок собран!\n"
                + $"   Токен: {data.TokenSymbol}\n"
                + $"   Количество: {data.Quantity:F0}"
        };
    }
}
