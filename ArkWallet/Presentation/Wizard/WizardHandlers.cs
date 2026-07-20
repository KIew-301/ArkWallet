using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Domain.ValueObjects;
using Microsoft.CodeAnalysis;

namespace ArkWallet.Infrastructure.Wizard
{
    partial class WizardEngine
    {
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
            string direction = session.Data["set_direction"].ToString();

            var validation = await _orderValidationService.ValidateTokenAsync(
                session.Id,
                input,
                direction
            );

            if (!validation.IsValid)
                return StepResult.Error(validation.Message);

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

            validation = await _orderValidationService.ValidateOrderCreationAsync(
                session.Id,
                symbol,
                direction,
                quantity,
                price
            );

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

            var orderDescription = data.Order.GetDesctiption();

            var message = data.IsFilled
                ? $"Ордер {orderDescription} успешно выставлен и уже исполнен."
                : $"Ордер {orderDescription} успешно выставлен.";

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
                ? StepResult.Ok("completed", result.Message)
                : StepResult.Error(result.Message);
        }

        public async Task<StepResult> HandleConfirmCancellationAllOrders(UserSession session, string input)
        {
            if (input != "confirm")
                return StepResult.Ok("completed", "Отмена не подтверждена");

            var result = await _cancelOrderService.CancelAllOrderAsync(session.Id);

            return result.IsSuccess
                ? StepResult.Ok("completed", result.Message)
                : StepResult.Error(result.Message);
        }

        public async Task<StepResult> HandleGetProfile(UserSession session, string input)
        {
            var profileResult = await _traderQueryService.GetTraderProfileAsync(session.Id);
            var portfolioQueryResult = await _portfolioQueryService.GetTraderTokensAsync(session.Id);
            string result = "";

            if (!profileResult.TryGetData(out var profile))
                return StepResult.Ok("completed", profileResult.Message ?? "Данные профиля не найдены.");

            if (!portfolioQueryResult.TryGetData(out var portfolioInfo))
                return StepResult.Ok("Данные профиля не найдены.");

            var result = $"{profile.Username}!\n" +
                $"{MakeIndent(3)}Баланс: {profile.Balance:F2}\n" +
                $"{MakeIndent(3)}Портфель:\n";

            if (portfolioInfo == null || portfolioInfo.Length <= 0)
            {
                result += $"{MakeIndent(6)}Не владеет токенами".PadLeft(3);
                return StepResult.Ok("completed", result);
            }

            result += string.Join("\n", portfolioInfo.Select(p => $"{MakeIndent(6)}{p.TokenInfo?.Symbol ?? "???"} - {p.Quantity} шт."));

            return StepResult.Ok("completed", result);
        }

        public async Task<StepResult> HandleSelectTokenInfo(UserSession session, string input)
            => await ValidateAndStoreToken(session, input, "show_info");

        public async Task<StepResult> HandleShowTokenInfo(UserSession session, string input)
        {
            var symbol = session.Data["token_symbol"]?.ToString();

            if (string.IsNullOrEmpty(symbol))
                return StepResult.Ok("completed", "Токен не выбран.");

            var tokenResult = await _tokenQueryService.GetTokenInfoAsync(symbol);

            if (!tokenResult.TryGetData(out var tokenData))
                return StepResult.Ok("completed", "Токен не найден.");

            var result = $"📊 Информация о токене\n" +
                $"{MakeIndent(3)}Символ: {tokenData.Symbol}\n" +
                $"{MakeIndent(3)}Название: {tokenData.Name}\n" +
                $"{MakeIndent(3)}Цена: {tokenData.CurrentPrice:F2}\n";

            return StepResult.Ok("completed", result);
        }

        public async Task<StepResult> HandleSelectTokenForHistory(UserSession session, string input)
            => await ValidateAndStoreToken(session, input, "set_timeframe");

        public async Task<StepResult> HandleSetTimeframe(UserSession session, string input)
            => ValidateAndStorePositiveInt(session, input, "timeframe_minutes", "set_limit");

        public async Task<StepResult> HandleSetLimit(UserSession session, string input)
        {
            if (!int.TryParse(input, out var limit) || limit <= 0)
                return StepResult.Error("Необходимо ввести положительное целое число.");

            var symbol = session.Data["token_symbol"]?.ToString();
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
                $"{c.DateTime:dd-MM-yyyy HH:mm} - {c.ClosePrice:F2}");

            var message = $"📈 История цен {symbol} (шаг {timeframe} мин, {candles.Count} записей):\n\n"
                + string.Join("\n", lines);

            return StepResult.Ok("completed", message);
        }

        public async Task<StepResult> HandleSelectTokenForOrderBook(UserSession session, string input)
            => await ValidateAndStoreToken(session, input, "set_buy_count");

        public async Task<StepResult> HandleSetBuyCount(UserSession session, string input)
            => ValidateAndStorePositiveInt(session, input, "buy_count", "set_sell_count");

        public async Task<StepResult> HandleSetSellCount(UserSession session, string input)
        {
            if (!int.TryParse(input, out var sellCount) || sellCount <= 0)
                return StepResult.Error("Необходимо ввести положительное целое число.");

            var symbol = session.Data["token_symbol"]?.ToString();
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

        private async Task<(string?, List<QuickButton>?)> HandleQuickOrderBook(string symbolStr, string buyCountStr, string sellCountStr)
        {
            var symbol = symbolStr.ToUpper();

            if (!int.TryParse(buyCountStr, out var buyCount) || buyCount <= 0)
                return ("Необходимо ввести положительное целое число для количества покупок.", null);

            if (!int.TryParse(sellCountStr, out var sellCount) || sellCount <= 0)
                return ("Необходимо ввести положительное целое число для количества продаж.", null);

            var bookResult = await _orderBookService.GetOrderBookAsync(symbol, buyCount, sellCount);

            if (!bookResult.TryGetData(out var book))
                return ($"Ошибка получения стакана: {bookResult.Message}", null);

            var message = FormatOrderBookMessage(book);
            var buttons = CreateOrderBookRefreshButtons(symbol, buyCount, sellCount);

            return (message, buttons);
        }

        private static List<QuickButton> CreateOrderBookRefreshButtons(string symbol, int buyCount, int sellCount)
        {
            return new List<QuickButton>
            {
                new() { Text = "🔄 Обновить", Value = $"/get_order_book {symbol} {buyCount} {sellCount}" }
            };
        }

        private static string MakeIndent(int count) => new(' ', count);

        private async Task<StepResult> ValidateAndStoreToken(UserSession session, string input, string nextStep)
        {
            var tokenResult = await _tokenQueryService.GetTokenInfoAsync(input.ToUpper());

            if (!tokenResult.TryGetData(out _))
                return StepResult.Error("Токен не найден. Проверьте символ и попробуйте снова.");

            session.Data.Add("token_symbol", input.ToUpper());
            return StepResult.Ok(nextStep);
        }

        private static StepResult ValidateAndStorePositiveInt(UserSession session, string input, string key, string nextStep)
        {
            if (!int.TryParse(input, out var value) || value <= 0)
                return StepResult.Error("Необходимо ввести положительное целое число.");

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
                    lines.Add($"  {num}     {price}  × {qty}");
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
                    lines.Add($"  {num}     {price}  × {qty}");
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
    }
}
