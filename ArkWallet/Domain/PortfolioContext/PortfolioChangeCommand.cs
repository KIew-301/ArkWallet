namespace ArkWallet.Domain.PortfolioContext;

/// <summary>
/// Operation to apply to a portfolio position.
/// </summary>
public enum PortfolioChangeType
{
    /// <summary>Increases the quantity and recalculates the average buy price.</summary>
    Buy,

    /// <summary>Moves tokens from the available balance into reserve.</summary>
    Reserve,

    /// <summary>Moves tokens from reserve into the sold state.</summary>
    Sell,

    /// <summary>Returns tokens from reserve back to the available balance.</summary>
    Return,

    /// <summary>Removes tokens from the available balance without any return.</summary>
    Remove,

    /// <summary>
    /// Adds tokens to the available balance without changing the average buy price,
    /// as if the position had always held more tokens.
    /// </summary>
    Add
}

/// <summary>
/// Command describing a portfolio mutation. Carries every parameter needed to locate the
/// position and apply the change. Dispatched as a whole to the Position aggregate, which
/// decides the exact operation and enforces the business rules.
/// </summary>
public sealed record PortfolioChangeCommand(
    long TraderId,
    string Symbol,
    PortfolioChangeType Type,
    int Quantity,
    decimal Price);
