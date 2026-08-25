using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Infrastructure;
using ArkWallet.Infrastructure.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ArkWallet.Tests.HelpTools;

internal static class TestMediatorFactory
{
    public static IMediator Create(ArkWalletDbContext db, ITokenPriceCandleUpdateService candleUpdateService)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton(candleUpdateService);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<OrderPlacedEventHandler>());
        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }
}
