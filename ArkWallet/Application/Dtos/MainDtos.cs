using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Application.Dtos
{
    public record OrderDto(
        string Id,
        string Direction,
        string Symbol,
        int Quantity,
        decimal Price,
        string Status,
        DateTime CreatedAt
    );

    public record TokenBalanceDto(
        string Symbol,
        int Quantity,
        decimal CurrentPrice
    );

    public record TokenInfoDto(
        string Symbol,
        string Name,
        decimal CurrentPrice,
        decimal? PriceChange24h
    );

    public record TraderInfoDto(
        long Id,
        string Name,
        decimal Balance
    );

    public record PortfolioSummaryDto(
        decimal TotalBalance,
        List<TokenBalanceDto> Tokens
    );
}
