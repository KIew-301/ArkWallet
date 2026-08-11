using System.Security.Cryptography;

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
internal class WallBlockerEngine
{
    private const int LevelsPerSide = 5;
    private const decimal NearestOffsetMin = 0.03m;
    private const decimal NearestOffsetMax = 0.05m;
    private const decimal StepOffsetMin = 0.02m;
    private const decimal StepOffsetMax = 0.10m;

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
        var next = RandomNumberGenerator.GetInt32(0, 1_000_000_001);
        return min + (max - min) * next / 1_000_000_000m;
    }
}
