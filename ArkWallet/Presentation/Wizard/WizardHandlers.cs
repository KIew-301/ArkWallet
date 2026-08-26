using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Contracts.TradeServices;
using ArkWallet.Domain.ValueObjects;
using Microsoft.CodeAnalysis;

namespace ArkWallet.Infrastructure.Wizard
{
    partial class WizardEngine
    {
        private static readonly string RefreshButtonText = "🔄 Обновить";
        private static readonly string PositiveIntErrorMessage = "Необходимо ввести положительное целое число.";
        private static readonly string YouMarker = "   ← Вы";

        private async Task<StepResult> HandleSetName(UserSession session, string input)
        {
            var result = await _traderRegistrationService.RegisterTraderAsync(session.Id, input);

            if (result.IsSuccess)
                return StepResult.Ok("completed", "Отлично! Вы успешно зарегистрированы!");
            else
                return StepResult.Ok("completed", result.Message);
        }

        private async Task<StepResult> HandleSetDirection(UserSession session, string input)
        {
            input = input.ToLower();

            var validation = _orderValidationService.ValidateDirection(
                input
            );

            if (!validation.IsValid)
                return StepResult.Error(validation.Message);

            session.Data.Add(session.CurrentStep, input);
            return StepResult.Ok("set_token");
        }

        private async Task<StepResult> HandleSetToken(UserSession session, string input)
        {
            session.Data.Add(session.CurrentStep, input.ToUpper());
            return StepResult.Ok("set_quantity");
        }

        private async Task<StepResult> HandleSetTokenQuantity(UserSession session, string input)
        {
            string direction = session.Data["set_direction"].ToString().ToLower();
            string symbol = session.Data["set_token"].ToString().ToUpper();

            if (!int.TryParse(input, out var quantity))
                return StepResult.Error("Необходимо ввести целое число.");

            var validation = _orderValidationService.ValidateQuantity(
                quantity
            );

            if (!validation.IsValid)
                return StepResult.Error(validation.Message);

            session.Data.Add(session.CurrentStep, quantity);
            return StepResult.Ok("set_price");
        }

        private async Task<StepResult> HandleSetTokenPrice(UserSession session, string input)
        {
            if (!decimal.TryParse(input, out decimal price))
                return StepResult.Error("Необходимо ввести число (допустимо не целое).");

            string direction = session.Data["set_direction"].ToString().ToLower();
            int quantity = (int)session.Data["set_quantity"];
            string symbol = session.Data["set_token"].ToString().ToUpper();

            var validation = _orderValidationService.ValidatePrice(
                price
            );

            if (!validation.IsValid)
                return StepResult.Error(validation.Message);

            var command = new CreateOrderCommand(
                session.Id,
                direction,
                symbol,
                quantity,
                price
            );

            var result = await _orderCreationService.CreateOrderAsync(command);

            if (!result.TryGetData(out var data))
                return StepResult.Error(result.Message);

            var order = data.Order;
            var isBuy = order.Direction == Domain.ValueObjects.OrderType.Buy;

            var message = isBuy
                ? $"⏳ Ожидаем, когда вам продадут {order.Quantity} шт. токенов {order.Symbol} по {order.Price:F2}{Descriptor.CurrencySymbol}"
                : $"⏳ Ожидаем, когда у вас купят {order.Quantity} шт. токенов {order.Symbol} по {order.Price:F2}{Descriptor.CurrencySymbol}";

            return StepResult.Ok("completed", message);
        }

        public async Task<StepResult> HandleSelectOrderToCancel(UserSession session, string input)
        {
            var validation = await _orderValidationService.ValidateOrderCancellationAsync(
                session.Id,
                input
            );

            if (!validation.IsValid)
                return StepResult.Error(validation.Message);

            session.Data[session.CurrentStep] = input;
            return StepResult.Ok("confirm_cancellation");
        }

        public async Task<StepResult> HandleConfirmCancellation(UserSession session, string input)
        {
            if (input != "confirm")
                return StepResult.Ok("completed", "Отмена не подтверждена");

            var orderId = session.Data["select_order_to_cancel"]?.ToString();

            if (string.IsNullOrEmpty(orderId))
                return StepResult.Error("Ордер не найден");

            var result = await _cancelOrderService.CancelOrderAsync(session.Id, orderId);

            return result.IsSuccess
                ? StepResult.Ok("completed", "Ордер успешно отменён")
                : StepResult.Error(result.Message);
        }

        public async Task<StepResult> HandleConfirmCancellationAllOrders(UserSession session, string input)
        {
            if (input != "confirm")
                return StepResult.Ok("completed", "Отмена не подтверждена");

            var result = await _cancelOrderService.CancelAllOrderAsync(session.Id);

            if (!result.IsSuccess)
                return StepResult.Error(result.Message);

            return result.TryGetData(out var count)
                ? StepResult.Ok("completed", $"Всего успешно отменено ордеров: {count}")
                : StepResult.Ok("completed", "Все активные ордера успешно отменены");
        }

        public async Task<StepResult> HandleGetProfile(UserSession session, string input)
        {
            var profileResult = await _traderQueryService.GetTraderProfileAsync(session.Id);

            if (!profileResult.TryGetData(out var profile))
                return StepResult.Ok("completed", profileResult.Message ?? "Данные профиля не найдены.");

            var snapshotResult = await _balanceSnapshotService.TakeTotalTraderBalanceSnapshot(session.Id);
            decimal totalBalance = profile.Balance;

            if (snapshotResult.IsSuccess && snapshotResult.TryGetData(out var snapshot))
            {
                totalBalance = snapshot.totalBalance;
            }

            var portfolioQueryResult = await _portfolioQueryService.GetTraderTokensAsync(session.Id);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"👤 {profile.Username}");
            sb.AppendLine();
            sb.AppendLine($"💰 Баланс: {profile.Balance:F2}{Descriptor.CurrencySymbol}");
            sb.AppendLine($"📊 Общий баланс: {totalBalance:F2}{Descriptor.CurrencySymbol}");
            sb.AppendLine();
            sb.AppendLine("📦 Портфель:");

            if (!portfolioQueryResult.TryGetData(out var portfolioInfo) || portfolioInfo == null || portfolioInfo.Length <= 0)
            {
                sb.Append("    Пусто");
            }
            else
            {
                foreach (var p in portfolioInfo)
                {
                    var symbol = p.TokenInfo?.Symbol ?? "???";
                    var cost = p.Quantity * p.AverageBuyPrice;
                    var currentValue = p.BalanceInToken;
                    var profit = currentValue - cost;
                    var profitEmoji = profit >= 0 ? "📈" : "📉";

                    sb.AppendLine($"    {symbol}: {p.Quantity} шт. (куплено за {cost:F2}{Descriptor.CurrencySymbol})");
                    sb.AppendLine($"    {profitEmoji} Если продать сейчас: {profit:+0.00;-0.00}{Descriptor.CurrencySymbol}");
                }
            }

            var positionResult = await _leadersTopByBalanceQueryService.GetTraderPositionAsync(session.Id);
            if (positionResult.IsSuccess && positionResult.TryGetData(out var posData))
            {
                sb.AppendLine();
                sb.Append($"🏆 Рейтинг по балансу: #{posData.Position} из {posData.TotalTraders}");
            }

            var buttons = new List<QuickButton>
            {
                new() { Text = RefreshButtonText, Value = "/get_profile" }
            };

            var stepResult = StepResult.Ok("completed", sb.ToString());
            stepResult.Buttons = buttons;
            return stepResult;
        }

        public async Task<StepResult> HandleSelectTokenInfo(UserSession session, string input)
            => await ValidateAndStoreToken(session, input, "show_info");

        public async Task<StepResult> HandleShowTokenInfo(UserSession session, string input)
        {
            var symbol = session.Data[TokenSymbolDataKey]?.ToString();

            if (string.IsNullOrEmpty(symbol))
                return StepResult.Ok("completed", "Токен не выбран.");

            var tokenResult = await _tokenQueryService.GetTokenInfoAsync(symbol);

            if (!tokenResult.TryGetData(out var tokenData))
                return StepResult.Ok("completed", "Токен не найден.");

            var result = $"📊 Информация о токене\n" +
                $"{MakeIndent(3)}Символ: {tokenData.Symbol}\n" +
                $"{MakeIndent(3)}Название: {tokenData.Name}\n" +
                $"{MakeIndent(3)}Цена: {tokenData.CurrentPrice:F2}{Descriptor.CurrencySymbol}\n";

            return StepResult.Ok("completed", result);
        }

        public async Task<StepResult> HandleSelectTokenForHistory(UserSession session, string input)
            => await ValidateAndStoreToken(session, input, "set_timeframe");

        public static Task<StepResult> HandleSetTimeframe(UserSession session, string input)
            => Task.FromResult(ValidateAndStorePositiveInt(session, input, "timeframe_minutes", "set_limit"));

        public async Task<StepResult> HandleSetLimit(UserSession session, string input)
        {
            if (!int.TryParse(input, out var limit) || limit <= 0)
                return StepResult.Error(PositiveIntErrorMessage);

            var symbol = session.Data[TokenSymbolDataKey]?.ToString();
            var timeframe = (int)session.Data["timeframe_minutes"];

            if (string.IsNullOrEmpty(symbol))
                return StepResult.Ok("completed", "Токен не выбран.");

            var endDateTime = DateTime.UtcNow;
            var startDateTime = endDateTime.AddMinutes(-timeframe * limit);

            var candlesResult = await _candleOrchestrator.GetAggregatedCandlesAsync(
                symbol, startDateTime, endDateTime, timeframe);

            if (!candlesResult.TryGetData(out var candles) || candles.Count == 0)
                return StepResult.Ok("completed", $"Нет данных по свечам токена {symbol} за указанный период.");

            var lines = candles.Select(c =>
                $"{c.DateTime:dd-MM-yyyy HH:mm} - {c.ClosePrice:F2}{Descriptor.CurrencySymbol}");

            var message = $"📈 История цен {symbol} (шаг {timeframe} мин, {candles.Count} записей):\n\n"
                + string.Join("\n", lines);

            return StepResult.Ok("completed", message);
        }

        public async Task<StepResult> HandleSelectTokenForOrderBook(UserSession session, string input)
            => await ValidateAndStoreToken(session, input, "set_buy_count");

        public static Task<StepResult> HandleSetBuyCount(UserSession session, string input)
            => Task.FromResult(ValidateAndStorePositiveInt(session, input, "buy_count", "set_sell_count"));

        public async Task<StepResult> HandleSetSellCount(UserSession session, string input)
        {
            if (!int.TryParse(input, out var sellCount) || sellCount <= 0)
                return StepResult.Error(PositiveIntErrorMessage);

            var symbol = session.Data[TokenSymbolDataKey]?.ToString();
            var buyCount = (int)session.Data["buy_count"];

            if (string.IsNullOrEmpty(symbol))
                return StepResult.Ok("completed", "Токен не выбран.");

            var bookResult = await _orderBookService.GetOrderBookAsync(symbol, buyCount, sellCount);

            if (!bookResult.TryGetData(out var book))
                return StepResult.Ok("completed", $"Ошибка получения стакана: {bookResult.Message}");

            var message = FormatOrderBookMessage(book);
            var refreshButton = CreateOrderBookRefreshButtons(symbol, buyCount, sellCount);

            var result = StepResult.Ok("completed", message);
            result.Buttons = refreshButton;
            return result;
        }

        private async Task<WizardResult> HandleQuickOrderBook(string symbolStr, string buyCountStr, string sellCountStr)
        {
            var symbol = symbolStr.ToUpper();

            if (!int.TryParse(buyCountStr, out var buyCount) || buyCount <= 0)
                return new WizardResult { Message = "Необходимо ввести положительное целое число для количества покупок." };

            if (!int.TryParse(sellCountStr, out var sellCount) || sellCount <= 0)
                return new WizardResult { Message = "Необходимо ввести положительное целое число для количества продаж." };

            var bookResult = await _orderBookService.GetOrderBookAsync(symbol, buyCount, sellCount);

            if (!bookResult.TryGetData(out var book))
                return new WizardResult { Message = $"Ошибка получения стакана: {bookResult.Message}" };

            var message = FormatOrderBookMessage(book);
            var buttons = CreateOrderBookRefreshButtons(symbol, buyCount, sellCount);

            return new WizardResult { Message = message, Buttons = buttons };
        }

        private static List<QuickButton> CreateOrderBookRefreshButtons(string symbol, int buyCount, int sellCount)
        {
            return new List<QuickButton>
            {
                new() { Text = RefreshButtonText, Value = $"/get_order_book {symbol} {buyCount} {sellCount}" }
            };
        }

        private static string MakeIndent(int count) => new(' ', count);

        private async Task<StepResult> ValidateAndStoreToken(UserSession session, string input, string nextStep)
        {
            var tokenResult = await _tokenQueryService.GetTokenInfoAsync(input.ToUpper());

            if (!tokenResult.TryGetData(out _))
                return StepResult.Error("Токен не найден. Проверьте символ и попробуйте снова.");

            session.Data.Add(TokenSymbolDataKey, input.ToUpper());
            return StepResult.Ok(nextStep);
        }

        private static StepResult ValidateAndStorePositiveInt(UserSession session, string input, string key, string nextStep)
        {
            if (!int.TryParse(input, out var value) || value <= 0)
                return StepResult.Error(PositiveIntErrorMessage);

            session.Data.Add(key, value);
            return StepResult.Ok(nextStep);
        }

        private static string FormatOrderBookMessage(OrderBookResult book)
        {
            var lines = new List<string>();

            lines.Add($"Стакан ордеров {book.Symbol}");
            lines.Add("");

            if (book.Bids.Count == 0 && book.Asks.Count == 0)
            {
                lines.Add("Стакан пуст.");
                return string.Join("\n", lines);
            }

            var allPrices = book.Asks.Select(a => a.Price)
                .Concat(book.Bids.Select(b => b.Price));

            var maxIntLen = allPrices.Max(p => (int)Math.Floor(p)).ToString().Length;
            var maxDecPlaces = allPrices
                .Select(p => p.ToString("G").Contains('.') ? p.ToString("G").Split('.')[1].Length : 0)
                .Max();
            maxDecPlaces = Math.Max(maxDecPlaces, 2);

            if (maxDecPlaces > 6) maxDecPlaces = 6;

            var priceWidth = maxIntLen + 1 + maxDecPlaces;
            var priceFmt = new string('0', maxIntLen) + "." + new string('0', maxDecPlaces);

            var numWidth = 2;
            var qtyWidth = 2;
            var rowLen = numWidth + 1 + priceWidth + 3 + qtyWidth + 2;

            if (book.Asks.Count > 0)
            {
                lines.Add("🔺 ПРОДАЖА (ASK)");
                var reversed = book.Asks.AsEnumerable().Reverse().ToList();
                for (int i = 0; i < reversed.Count; i++)
                {
                    var a = reversed[i];
                    var num = (reversed.Count - i).ToString().PadLeft(numWidth);
                    var price = a.Price.ToString(priceFmt).PadLeft(priceWidth);
                    var qty = a.Quantity.ToString().PadLeft(qtyWidth);
                    lines.Add($"  {num}     {price}{Descriptor.CurrencySymbol}  × {qty}");
                }
            }

            lines.Add(new string('─', rowLen));

            if (book.Bids.Count > 0)
            {
                for (int i = 0; i < book.Bids.Count; i++)
                {
                    var b = book.Bids[i];
                    var num = (i + 1).ToString().PadLeft(numWidth);
                    var price = b.Price.ToString(priceFmt).PadLeft(priceWidth);
                    var qty = b.Quantity.ToString().PadLeft(qtyWidth);
                    lines.Add($"  {num}     {price}{Descriptor.CurrencySymbol}  × {qty}");
                }
                lines.Add("🔻 ПОКУПКА (BID)");
            }

            lines.Add("");
            lines.Add("ℹ️ КАК ЧИТАТЬ:");
            lines.Add("[номер] [цена] × [количество]");
            lines.Add("— кто, по какой цене, сколько");
            lines.Add("   хочет купить или продать");
            lines.Add("      прямо сейчас");

            return string.Join("\n", lines);
        }

        private async Task<StepResult> HandleGetOrders(UserSession session, string input)
        {
            var ordersResult = await _orderQueryService.GetTraderOrdersAsync(
                session.Id, includeActive: true, includeFilled: false, includeCancelled: false);

            if (!ordersResult.IsSuccess || !ordersResult.TryGetData(out var orders) || orders.Count == 0)
                return StepResult.Ok("completed", "У вас нет активных ордеров.");

            var lines = new List<string>();
            lines.Add("📋 Ваши активные ордера:\n");

            foreach (var order in orders)
            {
                var directionEmoji = order.Direction == "Buy" ? "🟢" : "🔴";
                var directionText = order.Direction == "Buy" ? "Покупка" : "Продажа";
                var progress = order.FillPercent;
                var progressBar = CreateProgressBar(progress);

                lines.Add($"{directionEmoji} {directionText} {order.Symbol}");
                lines.Add($"   Цена: {order.Price:F2}{Descriptor.CurrencySymbol} | Кол-во: {order.TotalQuantity}");
                lines.Add($"   Исполнено: {order.FilledQuantity}/{order.TotalQuantity} ({progress:F0}%)");
                lines.Add($"   {progressBar}");
                lines.Add("");
            }

            var message = string.Join("\n", lines);
            var buttons = new List<QuickButton>
            {
                new() { Text = RefreshButtonText, Value = "/get_orders" }
            };

            var result = StepResult.Ok("completed", message);
            result.Buttons = buttons;
            return result;
        }

        private async Task<StepResult> HandleSetTradesLimit(UserSession session, string input)
        {
            if (!int.TryParse(input, out var limit) || limit <= 0)
                return StepResult.Error(PositiveIntErrorMessage);

            limit = Math.Clamp(limit, 1, 100);

            var tradesResult = await _tradeQueryService.GetTraderTradesAsync(session.Id, withTokenInfo: true);

            if (!tradesResult.IsSuccess || !tradesResult.TryGetData(out var trades) || trades.Count == 0)
                return StepResult.Ok("completed", "У вас пока нет сделок.");

            var limitedTrades = trades.Take(limit).ToList();

            var message = FormatTradesMessage(limitedTrades);
            var buttons = CreateTradesRefreshButtons(limit);

            var result = StepResult.Ok("completed", message);
            result.Buttons = buttons;
            return result;
        }

        private async Task<StepResult> HandleSetTopsLimit(UserSession session, string input)
        {
            if (!int.TryParse(input, out var limit) || limit <= 0)
                return StepResult.Error(PositiveIntErrorMessage);

            limit = Math.Clamp(limit, 1, 20);

            var (message, error) = await BuildTopMessage(session.Id, limit);
            if (error != null)
                return StepResult.Ok("completed", error);

            var buttons = new List<QuickButton>
            {
                new() { Text = RefreshButtonText, Value = $"/get_tops {limit}" }
            };

            var result = StepResult.Ok("completed", message!);
            result.Buttons = buttons;
            return result;
        }

        private static string CreateProgressBar(decimal percent)
        {
            var filled = (int)(percent / 10);
            var empty = 10 - filled;
            return "[" + new string('█', filled) + new string('░', empty) + $"] {percent:F0}%";
        }

        private static string FormatTradesMessage(List<TradeInfo> trades)
        {
            var lines = new List<string>();
            lines.Add($"📊 Последние {trades.Count} сделок:\n");

            foreach (var trade in trades)
            {
                var isBuyer = trade.TraderRole == "Buyer";
                var roleEmoji = isBuyer ? "🟢" : "🔴";
                var roleText = isBuyer ? "Купил" : "Продал";
                var symbol = trade.TokenInfo?.Symbol ?? "???";

                var balanceChange = trade.Profit;
                var tokenChange = trade.Quantity;

                var balanceEmoji = balanceChange >= 0 ? "💰" : "💸";
                var tokenEmoji = "🪙";

                lines.Add($"{roleEmoji} {roleText} {symbol}");
                lines.Add($"   Цена: {trade.ExecutionPrice:F2} | Кол-во: {trade.Quantity}");
                lines.Add($"   {balanceEmoji} Баланс: {balanceChange:+0.00;-0.00}{Descriptor.CurrencySymbol} | {tokenEmoji} Токены: {(isBuyer ? "+" : "-")}{tokenChange} шт.");
                lines.Add($"   📅 {trade.TradeDateTime:dd.MM.yyyy HH:mm}");
                lines.Add("");
            }

            return string.Join("\n", lines);
        }

        private static List<QuickButton> CreateTradesRefreshButtons(int limit)
        {
            return new List<QuickButton>
            {
                new() { Text = RefreshButtonText, Value = $"/get_trades {limit}" }
            };
        }

        public async Task<StepResult> HandleGetTokens(UserSession session, string input)
        {
            var tokensResult = await _tokenQueryService.GetAllActiveTokensAsync();

            if (!tokensResult.TryGetData(out var tokens) || tokens.Count == 0)
                return StepResult.Ok("completed", "Нет доступных токенов.");

            var lines = new List<string> { "📊 Токены:\n" };

            foreach (var token in tokens.OrderBy(t => t.TokenInfo.Symbol))
            {
                var change = token.DailyChangePercent;
                var emoji = change >= 0 ? "🟢" : "🔴";
                var sign = change >= 0 ? "+" : "";
                lines.Add($"{emoji} {token.TokenInfo.Symbol,-8} {token.TokenInfo.CurrentPrice,10:F2}{Descriptor.CurrencySymbol}  ({sign}{change:F1}%)");
            }

            lines.Add("");
            lines.Add($"Всего: {tokens.Count} токенов");

            var buttons = new List<QuickButton>
            {
                new() { Text = RefreshButtonText, Value = "/get_tokens" }
            };

            var result = StepResult.Ok("completed", string.Join("\n", lines));
            result.Buttons = buttons;
            return result;
        }

        private async Task<WizardResult> HandleQuickTrades(long userId, string limitStr)
        {
            if (!int.TryParse(limitStr, out var limit) || limit <= 0)
                return new WizardResult { Message = PositiveIntErrorMessage };

            limit = Math.Clamp(limit, 1, 100);

            var tradesResult = await _tradeQueryService.GetTraderTradesAsync(userId, withTokenInfo: true);
            if (!tradesResult.IsSuccess || !tradesResult.TryGetData(out var trades) || trades.Count == 0)
                return new WizardResult { Message = "У вас пока нет сделок." };

            var limitedTrades = trades.Take(limit).ToList();
            var message = FormatTradesMessage(limitedTrades);
            var buttons = CreateTradesRefreshButtons(limit);

            return new WizardResult { Message = message, Buttons = buttons };
        }

        private async Task<WizardResult> HandleQuickTops(long userId, string limitStr)
        {
            if (!int.TryParse(limitStr, out var limit) || limit <= 0)
                return new WizardResult { Message = PositiveIntErrorMessage };

            limit = Math.Clamp(limit, 1, 20);

            var (message, error) = await BuildTopMessage(userId, limit);
            if (error != null)
                return new WizardResult { Message = error };

            var buttons = new List<QuickButton>
            {
                new() { Text = RefreshButtonText, Value = $"/get_tops {limit}" }
            };

            return new WizardResult { Message = message, Buttons = buttons };
        }

        private async Task<(string? Message, string? Error)> BuildTopMessage(long userId, int limit)
        {
            var topResult = await _leadersTopByBalanceQueryService.GetTopAsync(limit);
            if (!topResult.IsSuccess || !topResult.TryGetData(out var top) || top.Count == 0)
                return (null, "Рейтинг пока пуст.");

            var lines = new List<string>();
            lines.Add($"🏆 Топ-{top.Count} трейдеров:");
            lines.Add("");

            foreach (var entry in top)
            {
                var medal = GetMedal(entry.Position);
                var meMarker = entry.TraderId == userId ? YouMarker : "";
                lines.Add($"{medal} {entry.Username} — {entry.TotalBalance:F2}{Descriptor.CurrencySymbol}{meMarker}");
            }

            await AppendLocalTop(lines, userId);

            return (string.Join("\n", lines), null);
        }

        private async Task AppendLocalTop(List<string> lines, long userId)
        {
            var localResult = await _leadersTopByBalanceQueryService.GetLocalTopAsync(userId, 2, 2);
            if (!localResult.IsSuccess || !localResult.TryGetData(out var local) || local.Count <= 1)
                return;

            lines.Add("");
            lines.Add("📍 Ваше окружение:");
            lines.Add("");
            foreach (var entry in local)
            {
                var marker = entry.TraderId == userId ? YouMarker : "";
                lines.Add($"  #{entry.Position} {entry.Username} — {entry.TotalBalance:F2}{Descriptor.CurrencySymbol}{marker}");
            }
        }

        private static string GetMedal(int position) => position switch
        {
            1 => "🥇",
            2 => "🥈",
            3 => "🥉",
            _ => $"#{position}"
        };
    }
}
