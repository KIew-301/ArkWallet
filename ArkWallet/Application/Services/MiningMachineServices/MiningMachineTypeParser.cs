using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Application.Services.MiningMachineServices;

/// <summary>
/// Парсер строкового типа майнинг-машины
/// </summary>
internal static class MiningMachineTypeParser
{
    public static MiningMachineType Parse(string? type)
    {
        return type?.ToUpperInvariant() switch
        {
            "SMAI" => MiningMachineType.SMAI,
            "MGC" => MiningMachineType.MGC,
            "BMP" => MiningMachineType.BMP,
            _ => throw new DomainException("Неизвестный тип машины")
        };
    }
}
