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

    public static MiningMachineRule Create(long miningMachineId, string characterTokenId, decimal miningCoefficient)
    {
        if (string.IsNullOrWhiteSpace(characterTokenId))
            throw new DomainException("Токен не указан");
        if (miningCoefficient <= 0)
            throw new DomainException("Коэффициент майнинга должен быть больше нуля");

        return new MiningMachineRule
        {
            MiningMachineId = miningMachineId,
            CharacterTokenId = characterTokenId,
            MiningCoefficient = miningCoefficient
        };
    }

    public void UpdateCoefficient(decimal miningCoefficient)
    {
        if (miningCoefficient <= 0)
            throw new DomainException("Коэффициент майнинга должен быть больше нуля");
        MiningCoefficient = miningCoefficient;
    }
}
