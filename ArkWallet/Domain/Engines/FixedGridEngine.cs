using System.Globalization;

namespace ArkWallet.Domain.Engines;

internal class FixedGridEngine
{
    private const decimal BASE_PRICE = 1000m;
    private const decimal STEP_MULTIPLIER = 1.001m;

    public List<decimal> GetGridAbovePrice(decimal currentPrice, int count = 10)
    {
        var index = FindClosestIndex(currentPrice);
        var grid = new List<decimal>();

        for (int i = index; i <= index + count - 1; i++)
        {
            grid.Add(GetGridValue(i));
        }

        return grid.OrderBy(x => x).ToList();
    }

    public List<decimal> GetGridBelowPrice(decimal currentPrice, int count = 10)
    {
        var index = FindClosestIndex(currentPrice);
        var grid = new List<decimal>();

        for (int i = index; i >= index - count + 1; i--)
        {
            grid.Add(GetGridValue(i));
        }

        return grid.OrderByDescending(x => x).ToList();
    }

    public List<decimal> GetGridAroundPrice(decimal currentPrice, int countAround = 5)
    {
        var grid = new List<decimal>();
        var index = FindClosestIndex(currentPrice);

        for (int i = index - countAround; i < index; i++)
        {
            if (i >= 0)
                grid.Add(GetGridValue(i));
        }

        for (int i = index; i < index + countAround; i++)
        {
            grid.Add(GetGridValue(i));
        }

        return grid.OrderBy(x => x).ToList();
    }

    private decimal GetGridValue(int index)
    {
        var value = BASE_PRICE * (decimal)Math.Pow((double)STEP_MULTIPLIER, index);
        return RoundToStep(value);
    }

    private int FindClosestIndex(decimal price)
    {
        if (price <= 0)
            return 0;

        return (int)Math.Round(Math.Log((double)(price / BASE_PRICE)) / Math.Log((double)STEP_MULTIPLIER));
    }

    public decimal RoundToStep(decimal value)
    {
        var step = value * 0.001m;
        var stepString = step.ToString(CultureInfo.InvariantCulture);

        var firstNonZeroIndex = -1;
        for (int i = 0; i < stepString.Length; i++)
        {
            if (char.IsDigit(stepString[i]) && stepString[i] != '0')
            {
                firstNonZeroIndex = i;
                break;
            }
        }

        if (firstNonZeroIndex == -1)
            return Math.Round(value, 0, MidpointRounding.AwayFromZero);

        var dotIndex = stepString.IndexOfAny(new[] { '.', ',' });
        var decimalPlaces = 0;

        if (dotIndex == -1)
        {
            var positionFromEnd = stepString.Length - firstNonZeroIndex - 1;
            decimalPlaces = -positionFromEnd;
        }
        else if (firstNonZeroIndex < dotIndex)
        {
            var positionFromEnd = dotIndex - firstNonZeroIndex - 1;
            decimalPlaces = -positionFromEnd;
        }
        else
        {
            decimalPlaces = firstNonZeroIndex - dotIndex;
        }

        if (decimalPlaces < 0)
        {
            var factor = (decimal)Math.Pow(10, -decimalPlaces);
            return Math.Round(value / factor, 0, MidpointRounding.AwayFromZero) * factor;
        }

        return Math.Round(value, decimalPlaces, MidpointRounding.AwayFromZero);
    }
}