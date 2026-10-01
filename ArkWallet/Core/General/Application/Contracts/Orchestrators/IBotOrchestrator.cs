using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

namespace ArkWallet.Core.General.Application.Contracts.Orchestrators;

/// <summary>
/// Оркестратор ботов: управляет жизненным циклом маркет-мейкеров и стены.
/// Не содержит бизнес-логики ботов — все решения принимают сами боты через
/// коллекции модификаторов (ExecutePlan / RebalancePower).
/// Размещение собранных ботом коллекций ордеров выполняет <see cref="IPlanModifierCollection"/>-синхронный шаг PlaceCollected.
/// </summary>
public interface IBotOrchestrator
{
    /// <summary>Обновляет балансы и портфели всех ботов до дефолтных значений бота.</summary>
    Task<Result> UpdateAllBotsBalancesAsync(CancellationToken ct = default);

    /// <summary>Обновляет сетки buyer/seller: каждый бот выполняет план (ExecutePlan) с сеточными модификаторами.</summary>
    Task<Result> UpdateBotsGridsAsync(CancellationToken ct = default);

    /// <summary>Активирует рыночные ордера buyer/seller: каждый бот выполняет план (ExecutePlan) с рыночными модификаторами.</summary>
    Task<Result> ExecuteMarketOrdersAsync(CancellationToken ct = default);

    /// <summary>Обновляет сетки wall-бота: каждый бот выполняет план (ExecutePlan) с модификаторами стены.</summary>
    Task<Result> UpdateWallBotGridsAsync(CancellationToken ct = default);

    /// <summary>Пересчитывает активную мощность всех ботов (RebalancePower) с модификаторами мощности.</summary>
    Task<Result> RebalanceAllBotsPowerAsync(CancellationToken ct = default);

    /// <summary>
    /// Гарантирует дефолтный состав ботов для каждого активного символа: Buyer, Seller и Waller.
    /// Для каждого отсутствующего бота создаёт его с отдельным трейдером. Повторный вызов безопасен (идемпотентен).
    /// </summary>
    Task<Result> EnsureDefaultBotsAsync(CancellationToken ct = default);
}

/// <summary>
/// Поставщик коллекций модификаторов по операциям оркестратора.
/// Открыт для расширения: конкретные модификаторы подключаются точечно по мере реализации.
/// </summary>
internal interface IPlanModifierCollection
{
    /// <summary>Модификаторы сеток buyer/seller.</summary>
    IReadOnlyCollection<IPlanModify> GridModifiers { get; }

    /// <summary>Модификаторы рыночных ордеров buyer/seller.</summary>
    IReadOnlyCollection<IPlanModify> MarketModifiers { get; }

    /// <summary>Модификаторы сеток стены.</summary>
    IReadOnlyCollection<IPlanModify> WallGridModifiers { get; }

    /// <summary>Модификаторы пересчёта активной мощности обычных ботов (buyer/seller).</summary>
    IReadOnlyCollection<IPowerCalculationModify> PowerModifiers { get; }

    /// <summary>Модификаторы пересчёта активной мощности стены (усиление ×20).</summary>
    IReadOnlyCollection<IPowerCalculationModify> WallerPowerModifiers { get; }
}