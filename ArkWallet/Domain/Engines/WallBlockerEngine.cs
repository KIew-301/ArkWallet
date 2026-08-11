namespace ArkWallet.Domain.Engines;

/// <summary>
/// Уровень сетки WallBlocker-бота: цена и направление ордера
/// </summary>
/// <param name="Price">Цена уровня</param>
/// <param name="Direction">Направление ордера ("купить" / "продать")</param>
internal record WallBlockerLevel(decimal Price, string Direction);

/// <summary>
/// Движок генерации уровней для MarketWallBlocker-бота
/// </summary>
internal class WallBlockerEngine(Random? random = null)
{
    private const int LevelsPerSide = 5;
    private const decimal NearestOffsetMin = 0.03m;
    private const decimal NearestOffsetMax = 0.05m;
    private const decimal StepOffsetMin = 0.02m;
    private const decimal StepOffsetMax = 0.10m;

    private readonly Random _random = random ?? new Random();

    public List<WallBlockerLevel> GetLevels(decimal currentPrice)
    {
        if (currentPrice <= 0)
            return [];

        var nearestOffset = NextPercent(NearestOffsetMin, NearestOffsetMax);

        var lowerCumulative = nearestOffset;
        var upperCumulative = nearestOffset;

        var levels = new List<WallBlockerLevel>(LevelsPerSide * 2);

        for (int i = 0; i < LevelsPerSide; i++)
        {
            if (i > 0)
            {
                lowerCumulative += NextPercent(StepOffsetMin, StepOffsetMax);
                upperCumulative += NextPercent(StepOffsetMin, StepOffsetMax);
            }

            levels.Add(new WallBlockerLevel(
                FixedGridEngine.RoundToStep(currentPrice * (1m - lowerCumulative)),
                "купить"));

            levels.Add(new WallBlockerLevel(
                FixedGridEngine.RoundToStep(currentPrice * (1m + upperCumulative)),
                "продать"));
        }

        return levels;
    }

    private decimal NextPercent(decimal min, decimal max)
    {
        var range = (double)(max - min);
        return min + (decimal)(_random.NextDouble() * range);
    }
}
