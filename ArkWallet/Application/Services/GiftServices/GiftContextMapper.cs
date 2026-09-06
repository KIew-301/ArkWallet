using ArkWallet.Domain.GiftContext;
using Records = global::ArkWallet.Domain.Entities;

namespace ArkWallet.Application.Services.GiftServices;

/// <summary>
/// Transports data between persistence records and the aggregates of the Gift context.
/// </summary>
internal static class GiftContextMapper
{
    // ---- Records -> aggregate ----

    internal static Tokens ToTokens(Records.PortfolioItem source, decimal price) => new(
        source.CharacterTokenId,
        source.Quantity,
        price);

    internal static SentGift ToSentGift(Records.MailMessage source) => new(
        source.TraderId,
        source.CreatedAt);

    // ---- Aggregate -> records (sync) ----

    internal static void ApplyToRecord(Records.PortfolioItem target, Tokens source) => target.ApplyState(
        source.Quantity,
        target.SellingQuantity,
        target.ReserveQuantity,
        target.AverageBuyPrice,
        target.AverageSellPrice,
        target.AverageReservePrice);
}
