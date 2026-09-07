using ArkWallet.Application.Contracts.MailServices;
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
                + $"   Количество: {data.Quantity}"
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
}
