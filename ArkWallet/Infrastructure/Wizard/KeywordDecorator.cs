using ArkWallet.Contracts;
using ArkWallet.Entities;
using ArkWallet.ValueObjects;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Infrastructure.Wizard
{
    internal class KeywordDecorator
    {
        private readonly IUnitOfWork _uow;

        public KeywordDecorator(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<List<QuickButton>> Decorate(string stepName, List<QuickButton> baseKeyword, UserSession session)
        {
            return stepName switch
            {
                "set_token" => await DecorateTokenQuestion(baseKeyword, session),
                "set_price" => await DecoratePriceQuestion(baseKeyword, session),
                _ => baseKeyword
            };
        }

        private async Task<List<QuickButton>> DecorateTokenQuestion(List<QuickButton> baseKeyword, UserSession session)
        {
            baseKeyword = new();
            var tokens = await _uow.Portfolios.GetByTraderAsync(session.Id);
            foreach (var token in tokens)
            {
                baseKeyword.Add(new() { Text = token.CharacterTokenId, Value = token.CharacterTokenId });
            }

            return baseKeyword;
        }

        private async Task<List<QuickButton>> DecoratePriceQuestion(List<QuickButton> baseKeyword, UserSession session)
        {
            baseKeyword = new();
            string direction = session.Data["set_direction"].ToString().ToLower();
            int quantity = (int)session.Data["set_quantity"];
            string symbol = session.Data["set_token"].ToString().ToUpper();

            Trader? trader =
                await _uow.Traders.GetByIdAsync(session.Id);
            CharacterToken? token =
                await _uow.Tokens.GetByIdAsync(symbol);

            if (direction == "купить")
            {
                decimal optimalPrice = trader.Balance / quantity - 0.01M;
                decimal currentPrice = token.CurrentPrice;
                decimal noBestPrice = token.CurrentPrice * 1.05M;
                decimal closeBestPrice = token.CurrentPrice * 0.95M;
                decimal farBestPrice = token.CurrentPrice * 0.80M;

                decimal[] prices = [optimalPrice, currentPrice, noBestPrice, closeBestPrice, farBestPrice];
                prices = prices.Order().ToArray();

                foreach (var price in prices)
                {
                    if (price <= optimalPrice && price < currentPrice * 1.30M)
                        baseKeyword.Add(new() { Text = price.ToString("F2"), Value = price.ToString("F2") });
                }
            }
            else
            {
                decimal currentPrice = token.CurrentPrice;
                decimal noBestPrice = token.CurrentPrice * 0.95M;
                decimal closeBestPrice = token.CurrentPrice * 1.05M;
                decimal farBestPrice = token.CurrentPrice * 1.20M;

                decimal[] prices = [currentPrice, noBestPrice, closeBestPrice, farBestPrice];
                prices = prices.OrderDescending().ToArray(); ;

                foreach (var price in prices)
                {
                    baseKeyword.Add(new() { Text = price.ToString("F2"), Value = price.ToString("F2") });
                }
            }

            return baseKeyword;
        }
    }
}
