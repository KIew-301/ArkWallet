using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Wizard;
using ArkWallet.Presentation.Telegram;
using ArkWallet.Telegram;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Reflection;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ArkWallet.PerformanceTests.E2e;

internal sealed class BotFlow
{
    private readonly E2eHost _host;

    public BotFlow(E2eHost host) => _host = host;

    public async Task<WizardResult> WizardAsync(long userId, string input)
    {
        using var scope = _host.Services.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<WizardEngine>();
        return await engine.ProcessInput(userId, input);
    }

    public async Task<int> RunTelegramLevelAsync(long chatId, params string[] inputs)
    {
        var bot = _host.Services.GetRequiredService<TelegramBot>();

        var loadConfiguration = typeof(TelegramBot).GetMethod("LoadConfiguration", BindingFlags.Instance | BindingFlags.NonPublic)!;
        loadConfiguration.Invoke(bot, new object[] { new ConfigurationService(_host.Configuration) });

        var mock = new Mock<ITelegramBotClient>(MockBehavior.Loose);
        var handleUpdate = typeof(TelegramBot).GetMethod("HandleUpdateAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        foreach (var input in inputs)
        {
            var update = new Update
            {
                Message = new Message
                {
                    Chat = new Chat { Id = chatId },
                    Text = input,
                    From = new User { Id = chatId }
                }
            };

            await (Task)handleUpdate.Invoke(bot, new object[] { mock.Object, update, CancellationToken.None })!;
        }

        return mock.Invocations.Count(i => i.Method.Name == "SendMessage");
    }
}
