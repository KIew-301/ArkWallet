using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Domain.MiningContext;

internal class GlobalRule
{
    public long Id { get; private set; }
    public string TokenSymbol { get; }
    public decimal CurrentCoefficient { get; private set; }
    public decimal FutureCoefficient { get; private set; }
    public decimal BaseTokenMiningSpeed { get; private set; }

    private GlobalRule(
        string tokenSymbol,
        decimal currentCoefficient,
        decimal futureCoefficient,
        decimal baseTokenMiningSpeed)
    {
        TokenSymbol = tokenSymbol;
        CurrentCoefficient = currentCoefficient;
        FutureCoefficient = futureCoefficient;
        BaseTokenMiningSpeed = baseTokenMiningSpeed;
    }

    public static GlobalRule Create(
        string tokenSymbol,
        decimal currentCoefficient,
        decimal futureCoefficient,
        decimal baseTokenMiningSpeed)
    {
        if (string.IsNullOrWhiteSpace(tokenSymbol))
            throw new DomainException("Token symbol cannot be empty");
        if (currentCoefficient <= 0)
            throw new DomainException("Current coefficient must be greater than 0");
        if (futureCoefficient <= 0)
            throw new DomainException("Future coefficient must be greater than 0");
        if (baseTokenMiningSpeed <= 0)
            throw new DomainException("Base token mining speed must be greater than 0");

        return new GlobalRule(tokenSymbol, currentCoefficient, futureCoefficient, baseTokenMiningSpeed);
    }

    public void AdvanceCoefficient(decimal newFutureCoefficient)
    {
        if (newFutureCoefficient <= 0)
            throw new DomainException("Future coefficient must be greater than 0");

        CurrentCoefficient = FutureCoefficient;
        FutureCoefficient = newFutureCoefficient;
    }

    public void UpdateCoefficients(decimal currentCoefficient, decimal futureCoefficient)
    {
        if (currentCoefficient <= 0)
            throw new DomainException("Current coefficient must be greater than 0");
        if (futureCoefficient <= 0)
            throw new DomainException("Future coefficient must be greater than 0");

        CurrentCoefficient = currentCoefficient;
        FutureCoefficient = futureCoefficient;
    }

    public void UpdateBaseTokenMiningSpeed(decimal baseTokenMiningSpeed)
    {
        if (baseTokenMiningSpeed <= 0)
            throw new DomainException("Base token mining speed must be greater than 0");

        BaseTokenMiningSpeed = baseTokenMiningSpeed;
    }
}
