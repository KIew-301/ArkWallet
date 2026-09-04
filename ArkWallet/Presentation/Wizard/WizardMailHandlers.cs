using ArkWallet.Application.Contracts.MailServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.ValueObjects;
using Newtonsoft.Json;

namespace ArkWallet.Infrastructure.Wizard;

partial class WizardEngine
{
    private const string MailFilterUnread = "unread";
    private const string MailFilterReward = "reward";
    private const string MailFilterAll = "all";

    private const string MailStatusSent = "Sent";
    private const string MailStatusRead = "Read";
    private const string MailStatusAccepted = "Accepted";

    public async Task<WizardResult> HandleMailOpen(long userId)
    {
        var mailsResult = await _mailQueryService.GetUserMailsAsync(userId);
        if (!mailsResult.TryGetData(out var mails))
            return new WizardResult { Message = "Не удалось загрузить почту." };

        int unreadCount = mails.Count(m => m.Status == MailStatusSent);
        int rewardCount = mails.Count(m =>
            (m.Status == MailStatusSent || m.Status == MailStatusRead)
            && !string.IsNullOrEmpty(m.SymbolForReward) && m.AmountForReward > 0);

        var buttons = new List<QuickButton>
        {
            new() { Text = $"Непрочитанные ({unreadCount})", Value = $"/open_mail {MailFilterUnread}" },
            new() { Text = $"Награды ({rewardCount})", Value = $"/open_mail {MailFilterReward}" },
            new() { Text = $"Все ({mails.Count})", Value = $"/open_mail {MailFilterAll}" },
        };

        return new WizardResult { Message = "Почта открыта", Buttons = buttons };
    }

    public async Task<WizardResult> HandleMailList(long userId, string filter)
    {
        var mailsResult = await _mailQueryService.GetUserMailsAsync(userId);
        if (!mailsResult.TryGetData(out var mails))
            return new WizardResult { Message = "Не удалось загрузить почту." };

        var filtered = filter switch
        {
            MailFilterUnread => mails.Where(m => m.Status == MailStatusSent).ToList(),
            MailFilterReward => mails.Where(m =>
                (m.Status == MailStatusSent || m.Status == MailStatusRead)
                && !string.IsNullOrEmpty(m.SymbolForReward) && m.AmountForReward > 0).ToList(),
            _ => mails
        };

        if (filtered.Count == 0)
        {
            string emptyMsg = filter switch
            {
                MailFilterUnread => "Нет непрочитанных писем.",
                MailFilterReward => "Нет писем с наградами.",
                _ => "Почта пуста."
            };

            var backBtn = new List<QuickButton>
            {
                new() { Text = "Назад", Value = "/open_mail" }
            };

            return new WizardResult { Message = emptyMsg, Buttons = backBtn };
        }

        string filterLabel = filter switch
        {
            MailFilterUnread => "Непрочитанные",
            MailFilterReward => "Награды",
            _ => "Все"
        };

        var buttons = filtered.Take(20).Select(m =>
        {
            string statusIcon = m.Status == MailStatusSent ? "● " : "";
            string rewardIcon = !string.IsNullOrEmpty(m.SymbolForReward) && m.AmountForReward > 0 ? " 🎁" : "";
            string text = $"{statusIcon}{m.Title}{rewardIcon}";
            return new QuickButton { Text = text, Value = $"/open_mail open {m.Id}" };
        }).ToList();

        if (filter == MailFilterReward && filtered.Count > 1)
        {
            buttons.Add(new QuickButton { Text = "Собрать все", Value = "/open_mail accept_all" });
        }

        buttons.Add(new QuickButton { Text = "Назад", Value = "/open_mail" });

        string listHeader = $"Открыт: {filterLabel} ({filtered.Count})";

        return new WizardResult { Message = listHeader, Buttons = buttons };
    }

    public async Task<WizardResult> HandleMailRead(long userId, string mailIdStr)
    {
        if (!long.TryParse(mailIdStr, out var mailId))
            return new WizardResult { Message = "Неверный ID письма." };

        var mailsResult = await _mailQueryService.GetUserMailsAsync(userId);
        if (!mailsResult.TryGetData(out var mails))
            return new WizardResult { Message = "Не удалось загрузить почту." };

        var mail = mails.FirstOrDefault(m => m.Id == mailId);
        if (mail == null)
            return new WizardResult { Message = "Письмо не найдено." };

        if (mail.Status == MailStatusSent)
        {
            await _mailStatusUpdatingService.MarkAsReadAsync(mailId, userId);
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📩 {mail.Title}");
        sb.AppendLine();
        sb.AppendLine(mail.Message);

        if (!string.IsNullOrEmpty(mail.SymbolForReward) && mail.AmountForReward > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"🎁 Награда: {mail.AmountForReward} {mail.SymbolForReward}");
        }

        var buttons = new List<QuickButton>();

        bool hasReward = !string.IsNullOrEmpty(mail.SymbolForReward) && mail.AmountForReward > 0;
        bool notAccepted = mail.Status != MailStatusAccepted;

        if (hasReward && notAccepted)
        {
            buttons.Add(new QuickButton
            {
                Text = "Собрать награду",
                Value = $"/open_mail accept {mailId}"
            });
        }

        buttons.Add(new QuickButton { Text = "Назад", Value = "/open_mail" });

        return new WizardResult { Message = sb.ToString(), Buttons = buttons };
    }

    public async Task<WizardResult> HandleMailAccept(long userId, string mailIdStr)
    {
        if (!long.TryParse(mailIdStr, out var mailId))
            return new WizardResult { Message = "Неверный ID письма." };

        var mailsResult = await _mailQueryService.GetUserMailsAsync(userId);
        if (!mailsResult.TryGetData(out var mails))
            return new WizardResult { Message = "Не удалось загрузить почту." };

        var mail = mails.FirstOrDefault(m => m.Id == mailId);
        if (mail == null)
            return new WizardResult { Message = "Письмо не найдено." };

        if (mail.Status == MailStatusAccepted)
            return new WizardResult { Message = "Награда уже собрана." };

        if (string.IsNullOrEmpty(mail.SymbolForReward) || mail.AmountForReward <= 0)
            return new WizardResult { Message = "В этом письме нет награды." };

        var acceptResult = await _mailStatusUpdatingService.MarkAsAcceptedAsync(mailId, userId);
        if (!acceptResult.IsSuccess)
            return new WizardResult { Message = acceptResult.Message ?? "Не удалось собрать награду." };

        var buttons = new List<QuickButton>
        {
            new() { Text = "Назад", Value = "/open_mail" }
        };

        return new WizardResult
        {
            Message = $"✅ Награда собрана: {mail.AmountForReward} {mail.SymbolForReward}",
            Buttons = buttons
        };
    }

    public async Task<WizardResult> HandleMailAcceptAll(long userId)
    {
        var mailsResult = await _mailQueryService.GetUserMailsAsync(userId);
        if (!mailsResult.TryGetData(out var mails))
            return new WizardResult { Message = "Не удалось загрузить почту." };

        var rewardMails = mails.Where(m =>
            (m.Status == MailStatusSent || m.Status == MailStatusRead)
            && !string.IsNullOrEmpty(m.SymbolForReward) && m.AmountForReward > 0).ToList();

        if (rewardMails.Count == 0)
            return new WizardResult { Message = "Нет наград для сбора." };

        int accepted = 0;
        foreach (var m in rewardMails)
        {
            var result = await _mailStatusUpdatingService.MarkAsAcceptedAsync(m.Id, userId);
            if (result.IsSuccess)
                accepted++;
        }

        var buttons = new List<QuickButton>
        {
            new() { Text = "Назад", Value = "/open_mail" }
        };

        return new WizardResult
        {
            Message = $"✅ Собрано наград: {accepted} из {rewardMails.Count}",
            Buttons = buttons
        };
    }

    private async Task<StepResult> AdminHandleSendMail(UserSession session, string input)
    {
        try
        {
            var rawData = JsonConvert.DeserializeObject<Dictionary<string, object>>(input,
                new JsonSerializerSettings { FloatParseHandling = FloatParseHandling.Decimal });
            if (rawData == null)
                return StepResult.Error("Отправьте корректный JSON.");

            if (!rawData.TryGetValue("recipientId", out var recipientValue) || recipientValue == null)
                return StepResult.Error("recipientId обязателен.");

            List<long> recipientIds;
            if (recipientValue is Newtonsoft.Json.Linq.JArray array)
            {
                recipientIds = array
                    .Select(t => t.ToObject<long>())
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                if (recipientIds.Count == 0)
                    return StepResult.Error("recipientId (массив) должен содержать корректные Telegram ID.");
            }
            else
            {
                var recipientRaw = recipientValue.ToString()?.Trim();
                if (recipientRaw?.Equals("all", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var allResult = await _traderQueryService.GetAllTraderIdsAsync();
                    if (!allResult.TryGetData(out var ids) || ids.Count == 0)
                        return StepResult.Error("Нет зарегистрированных пользователей для рассылки.");

                    recipientIds = ids;
                }
                else if (long.TryParse(recipientRaw, out var singleId) && singleId > 0)
                {
                    recipientIds = [singleId];
                }
                else
                {
                    return StepResult.Error("recipientId должен быть Telegram ID, массивом ID или \"all\".");
                }
            }

            string title = rawData["title"]?.ToString() ?? string.Empty;
            string message = rawData["message"]?.ToString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
                return StepResult.Error("title и message не могут быть пустыми.");

            string rewardSymbol = rawData["rewardSymbol"]?.ToString() ?? string.Empty;
            decimal rewardAmount = rawData.TryGetValue("rewardAmount", out var amountRaw)
                ? Convert.ToDecimal(amountRaw, System.Globalization.CultureInfo.InvariantCulture)
                : 0m;

            bool hasReward = !string.IsNullOrWhiteSpace(rewardSymbol) && rewardAmount > 0;

            var commands = recipientIds
                .Select(id => new MailCreateCommand(
                    id,
                    title.Trim(),
                    message.Trim(),
                    "Администрация",
                    session.Id,
                    rewardSymbol,
                    rewardAmount,
                    hasReward ? "Reward" : "Notification"))
                .ToList();

            var createResult = await _mailMessageService.CreateManyAsync(commands);
            if (!createResult.TryGetData(out var created))
                return StepResult.Error(createResult.Message ?? "Не удалось отправить письма.");

            return StepResult.Ok("completed",
                $"✉️ Письма отправлены: {created.Count} пользователям" +
                (hasReward ? $"\n🎁 Награда: {rewardAmount} {rewardSymbol}" : ""));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.StackTrace);
            return StepResult.Error($"Ошибка: {ex.Message}");
        }
    }
}
