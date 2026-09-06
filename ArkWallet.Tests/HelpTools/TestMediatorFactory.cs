using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.MailServices;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Services.MailServices;
using ArkWallet.Infrastructure;
using ArkWallet.Infrastructure.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.HelpTools;

internal static class TestMediatorFactory
{
    public static IMediator Create(ArkWalletDbContext db, ITokenPriceCandleUpdateService candleUpdateService)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton(candleUpdateService);
        var taskDispatcher = new Mock<ITaskDispatcher>().Object;
        services.AddSingleton(taskDispatcher);
        services.AddSingleton<IMailMessageService>(new MailMessageService(
            db,
            taskDispatcher,
            NullLogger<MailMessageService>.Instance,
            TimeProvider.System));
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<OrderPlacedEventHandler>());
        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }
}
