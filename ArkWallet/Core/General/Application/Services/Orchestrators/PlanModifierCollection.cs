using ArkWallet.Core.General.Application.Contracts.Orchestrators;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

namespace ArkWallet.Core.General.Application.Services.Orchestrators;

/// <summary>
/// Поставщик коллекций модификаторов по операциям оркестратора.
/// Порядок модификаторов имеет значение: сначала источники ордеров, затем трансформаторы quantity.
/// Модификаторы stateless (см. комментарий), поэтому коллекция формируется на статических инстансах.
/// </summary>
internal sealed class PlanModifierCollection : IPlanModifierCollection
{
    private static readonly BuyerGridModifier BuyerGrid = new();
    private static readonly SellerGridModifier SellerGrid = new();
    private static readonly WallGridModifier WallGrid = new();
    private static readonly MarketOrderModifier MarketOrder = new();
    private static readonly GridReductionModifier GridReduction = new();
    private static readonly OrderRandomnessModifier Randomness = new();
    private static readonly OrderImpulseModifier Impulse = new();
    private static readonly DailyCorrectionModifier DailyCorrection = new();
    private static readonly RandomnessPowerModifier RandomnessPower = new();
    private static readonly AmplificationPowerModifier AmplificationPower = new();

    public IReadOnlyCollection<IPlanModify> GridModifiers { get; } = new IPlanModify[]
    {
        BuyerGrid, SellerGrid, GridReduction, Randomness,
    };

    public IReadOnlyCollection<IPlanModify> MarketModifiers { get; } = new IPlanModify[]
    {
        MarketOrder, Impulse, Randomness, DailyCorrection,
    };

    public IReadOnlyCollection<IPlanModify> WallGridModifiers { get; } = new IPlanModify[]
    {
        WallGrid, Randomness,
    };

    public IReadOnlyCollection<IPowerCalculationModify> PowerModifiers { get; } = new IPowerCalculationModify[]
    {
        RandomnessPower,
    };

    public IReadOnlyCollection<IPowerCalculationModify> WallerPowerModifiers { get; } = new IPowerCalculationModify[]
    {
        RandomnessPower, AmplificationPower,
    };
}
