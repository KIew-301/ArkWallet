using ArkWallet.Domain.Engines;

namespace ArkWallet.Tests.EngineTests;

public class FixedGridEngineTest
{
    private readonly FixedGridEngine _engine = new();

    [Fact]
    public void GetGridBelowPrice_From1000_Count20_ReturnsCorrectValues()
    {
        var result = _engine.GetGridBelowPrice(1000m, 20);

        var expected = new[]
        {
            1000m, 999m, 998m, 997m, 996.0m,
            995.0m, 994.0m, 993.0m, 992.0m, 991.0m,
            990.1m, 989.1m, 988.1m, 987.1m, 986.1m,
            985.1m, 984.1m, 983.2m, 982.2m, 981.2m
        };

        Assert.Equal(expected.Length, result.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], result[i]);
        }
    }

    [Fact]
    public void GetGridAbovePrice_From1000_Count20_ReturnsCorrectValues()
    {
        var result = _engine.GetGridAbovePrice(1000m, 20);

        var expected = new[]
        {
            1000m, 1001m, 1002m, 1003m, 1004m,
            1005m, 1006m, 1007m, 1008m, 1009m,
            1010m, 1011m, 1012m, 1013m, 1014m,
            1015m, 1016m, 1017m, 1018m, 1019m
        };

        Assert.Equal(expected.Length, result.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], result[i]);
        }
    }

    [Fact]
    public void GetGridAroundPrice_From1000_ShouldContain_GetGridBelowPrice_From990_5_10Values()
    {
        var around1000 = _engine.GetGridBelowPrice(1000m, 20);
        var below990_5 = _engine.GetGridBelowPrice(990.5m, 10);

        foreach (var value in below990_5)
        {
            Assert.Contains(value, around1000);
        }
    }

    [Fact]
    public void GetGridAroundPrice_From1000_ShouldContain_GetGridAbovePrice_From1011_10Values()
    {
        var around1000 = _engine.GetGridAbovePrice(1000m, 21);
        var above1011 = _engine.GetGridAbovePrice(1011m, 10);

        foreach (var value in above1011)
        {
            Assert.Contains(value, around1000);
        }
    }

    [Fact]
    public void RoundToStep_ShouldRoundCorrectlyBasedOnStep()
    {
        var testCases = new[]
        {
            new { Value = 1000.041m, Expected = 1000m },
            new { Value = 1000.999m, Expected = 1001m },
            new { Value = 55.555m, Expected = 55.56m },
            new { Value = 0.013151245m, Expected = 0.01315m },
            new { Value = 999.999m, Expected = 1000m },
            new { Value = 0.0001m, Expected = 0.0001m },
            new { Value = 55555m, Expected = 55560m }
        };

        foreach (var testCase in testCases)
        {
            var result = FixedGridEngine.RoundToStep(testCase.Value);
            Assert.Equal(testCase.Expected, result);
        }
    }
}