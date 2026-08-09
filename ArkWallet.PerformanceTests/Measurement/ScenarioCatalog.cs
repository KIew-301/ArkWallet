namespace ArkWallet.PerformanceTests.Measurement;

public sealed record ScenarioDefinition(
    string Id,
    string Title,
    string Kind,
    string Description,
    bool Implemented,
    IReadOnlyDictionary<string, string> Conditions);

internal static class ScenarioCatalog
{
    public static IReadOnlyList<ScenarioDefinition> All { get; } = Build();

    public static ScenarioDefinition? GetById(string id)
        => All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<ScenarioDefinition> Build()
    {
        return new[]
        {
            Service("token-query-50t",
                "TokenQueryService · GetAllActiveTokensAsync",
                "Каталог из 50 активных токенов (полный список).",
                C(("Токены", "50"), ("Свечи", "50"))),

            Service("balance-main-changes",
                "BalanceChangesCalculationService · TakeMainBalanceChanges",
                "Смена основного баланса трейдера за период (снапшоты + портфель).",
                C(("Трейдеры", "1"), ("Снапшоты баланса", "2"), ("Токены", "1"), ("Портфель", "1 позиция"))),

            Service("balance-total-changes",
                "BalanceChangesCalculationService · TakeTotalBalanceChanges",
                "Смена общего баланса трейдера за период (снапшоты + портфель).",
                C(("Трейдеры", "1"), ("Снапшоты баланса", "2"), ("Токены", "1"), ("Портфель", "1 позиция"))),

            Service("leaders-top-50t",
                "LeadersTopByBalanceQueryService · GetTopAsync(10)",
                "Топ-10 трейдеров по балансу (пересчёт по портфелям).",
                C(("Трейдеры", "50"), ("Токены", "50"), ("Свечи", "50"), ("Портфель", "50 позиций"), ("TopLimit", "10"))),

            Service("order-create-buy",
                "OrderCreationService · CreateOrderAsync (buy)",
                "Создание buy-ордера полным путём: валидация + движок + свеча.",
                C(("Трейдеры", "1"), ("Токены", "1"), ("Свечи", "1"), ("Портфель", "1 позиция"), ("Ордер", "1 buy"))),

            Service("order-create-sell",
                "OrderCreationService · CreateOrderAsync (sell)",
                "Создание sell-ордера полным путём: валидация + движок + свеча.",
                C(("Трейдеры", "1"), ("Токены", "1"), ("Свечи", "1"), ("Портфель", "1 позиция"), ("Ордер", "1 sell"))),

            Service("market-maker-tick-10t",
                "MarketMakerOrchestrator · ProcessBotsAsync (10 токенов)",
                "Полный тик маркет-мейкера на 10 токенах.",
                C(("Токены", "10"), ("Свечи", "10"), ("MM-боты", "20 (2 на токен)"), ("Трейдеры", "2"),
                    ("Sell-ордера", "20"), ("Портфель", "20 позиций"))),

            Service("market-maker-tick-20t",
                "MarketMakerOrchestrator · ProcessBotsAsync (20 токенов)",
                "Полный тик маркет-мейкера на 20 токенах.",
                C(("Токены", "20"), ("Свечи", "20"), ("MM-боты", "40 (2 на токен)"), ("Трейдеры", "2"),
                    ("Sell-ордера", "40"), ("Портфель", "40 позиций"))),

            E2e("e2e-dashboard-flow",
                "E2E API · Dashboard flow",
                "login → GET /tokens → GET /candles → GET /orders → GET /portfolios → GET /balance.",
                C(("Токены", "50"), ("Свечи", "50"), ("Трейдеры", "1"), ("Сеть", "выкл. (WebApplicationFactory)"))),

            E2e("e2e-trading-flow",
                "E2E API · Trading flow",
                "login → POST /orders (buy) → GET /orders → GET /trades → DELETE /orders/{id}.",
                C(("Токены", "1"), ("Трейдеры", "1"), ("Ордер", "1 buy"), ("Сеть", "выкл."))),

            E2e("e2e-cache-check",
                "E2E API · Cache check",
                "Двойной вызов GET /tokens при включённом IMemoryCache-декораторе → 0 запросов БД на 2-й вызов.",
                C(("Прогоны", "2"), ("Цель 2-го прогона", "0 запросов БД"), ("IMemoryCache", "вкл."))),

            E2e("e2e-bot-wizard-flow",
                "E2E Bot · Wizard flow",
                "/start → /place_order (4 шага) → /get_orders → /get_profile.",
                C(("Трейдеры", "1"), ("Токены", "50"), ("Команды", "4"), ("Вводы", "4"))),

            E2e("e2e-bot-admin-flow",
                "E2E Bot · Admin flow",
                "/admin_bots_activity → TKN000 → /admin_stats → /admin_get_ids.",
                C(("Трейдеры", "1 (не-бот)"), ("MM-боты", "10"), ("Команды", "3"), ("Вводы", "1"))),

            E2e("e2e-telegram-bot-level",
                "E2E Bot · TelegramBot level",
                "TelegramBot с фейковым ITelegramBotClient: 4 команды подряд; замер от Update до ответа (без сети).",
                C(("Команды", "4"), ("Сеть", "выкл. (фейковый ITelegramBotClient)"))),

            E2e("heavy-orders-get",
                "E2E API · GET /orders (10k ордеров)",
                "Список ордеров трейдера: 10k ордеров (Active/Filled/Cancelled) на 100 токенах.",
                C(("Ордера", "10 000"), ("Токены", "100"), ("Статусы", "Active/Filled/Cancelled"))),

            E2e("heavy-order-create",
                "E2E API · POST /orders (книга 2k ask)",
                "Создание buy-ордера при 2k активных ask в книге TKN000 (полный путь: валидация + движок + свеча).",
                C(("Книга (ask)", "2 000"), ("Трейдеры", "2"), ("Токены", "1"))),

            E2e("heavy-orders-cancel-all",
                "E2E API · DELETE /orders (2k отмена)",
                "Отмена всех активных ордеров: 2k ордеров, 1.6k short на 100 символах (батч портфеля + bulk UPDATE).",
                C(("Ордера", "2 000"), ("Short", "1 600"), ("Символы", "100"))),

            E2e("heavy-tokens-get",
                "E2E API · GET /tokens (500 токенов)",
                "Список активных токенов: 500 токенов (N+1: 2 запроса на токен).",
                C(("Токены", "500"), ("Свечи", "500"))),

            E2e("heavy-candle-get",
                "E2E API · GET /candle (100k свечей)",
                "Свеча за 100 дней: 100k свечей, агрегация до timeframe=60 (почасовая).",
                C(("Свечи", "100 000"), ("Период", "100 дней"), ("Таймфрейм", "60 мин"))),

            E2e("heavy-balance-get",
                "E2E API · GET /balance (10k снапшотов)",
                "Баланс: 10k снапшотов, 500 позиций портфеля, 1k активных ордеров.",
                C(("Снапшоты", "10 000"), ("Портфель", "500 позиций"), ("Ордера", "1 000"))),

            E2e("heavy-trades-get",
                "E2E API · GET /trades (20k сделок)",
                "История сделок трейдера: 20k сделок (Buyer/Seller) по 100 токенам.",
                C(("Сделки", "20 000"), ("Токены", "100"))),
        };
    }

    private static ScenarioDefinition Service(string id, string title, string description, IReadOnlyDictionary<string, string> conditions)
        => new(id, title, "Сервис", description, true, conditions);

    private static ScenarioDefinition Planned(string id, string title, string description, IReadOnlyDictionary<string, string> conditions)
        => new(id, title, "E2E", description, false, conditions);

    private static ScenarioDefinition E2e(string id, string title, string description, IReadOnlyDictionary<string, string> conditions)
        => new(id, title, "E2E", description, true, conditions);

    private static IReadOnlyDictionary<string, string> C(params (string Key, string Value)[] pairs)
    {
        var result = new Dictionary<string, string>(pairs.Length);
        foreach (var (key, value) in pairs)
            result[key] = value;
        return result;
    }
}
