using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Domain.Entities;

/// <summary>
/// Правило майнинга: коэффициент добычи конкретного токена на конкретной машине
/// </summary>
internal class MiningMachineRule
{
    public long Id { get; private set; }
    public long MiningMachineId { get; private set; }
    public string CharacterTokenId { get; private set; } = string.Empty;
    public decimal MiningCoefficient { get; private set; }

    public virtual MiningMachine? MiningMachine { get; set; }
    public virtual CharacterToken? CharacterToken { get; set; }

    private const decimal MinMiningCoefficient = 0.65m;
    private const decimal MaxMiningCoefficient = 1m;

    public static MiningMachineRule Create(long miningMachineId, string characterTokenId, decimal miningCoefficient)
    {
        if (string.IsNullOrWhiteSpace(characterTokenId))
            throw new DomainException("Токен не указан");
        ValidateCoefficient(miningCoefficient);

        return new MiningMachineRule
        {
            MiningMachineId = miningMachineId,
            CharacterTokenId = characterTokenId,
            MiningCoefficient = miningCoefficient
        };
    }

    public void UpdateCoefficient(decimal miningCoefficient)
    {
        ValidateCoefficient(miningCoefficient);
        MiningCoefficient = miningCoefficient;
    }

    private static void ValidateCoefficient(decimal miningCoefficient)
    {
        if (miningCoefficient < MinMiningCoefficient || miningCoefficient > MaxMiningCoefficient)
            throw new DomainException($"Коэффициент майнинга должен быть от {MinMiningCoefficient} до {MaxMiningCoefficient}");
    }
}
