namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>Случайность мощности: активная сила = базовая × равномерный [0.5, 1.5).</summary>
internal sealed class RandomnessPowerModifier : IPowerCalculationModify
{
    public void Apply(MarketMakerBot bot)
    {
        var factor = 0.5m + (decimal)Random.Shared.NextDouble();
        bot.SetActivePower(bot.BasePower * factor);
    }
}

/// <summary>Усиление: повышает фактическую (текущую активную) силу в 8 раз.</summary>
internal sealed class AmplificationPowerModifier : IPowerCalculationModify
{
    private const int Multiplier = 8;

    public void Apply(MarketMakerBot bot)
        => bot.SetActivePower(bot.ActivePower * Multiplier);
}
