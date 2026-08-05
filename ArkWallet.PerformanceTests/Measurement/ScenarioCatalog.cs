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

            Planned("e2e-dashboard-flow",
                "E2E API · Dashboard flow",
                "login → GET /tokens → GET /tokens/{id} → GET /candles → GET /portfolios → GET /balance.",
                C(("Токены", "50"), ("Свечи", "50"), ("Трейдеры", "1"), ("Сеть", "выкл. (WebApplicationFactory)"))),

            Planned("e2e-trading-flow",
                "E2E API · Trading flow",
                "login → POST /orders (buy) → GET /orders → GET /trades → DELETE /orders/{id}.",
                C(("Токены", "1"), ("Трейдеры", "1"), ("Ордер", "1 buy"), ("Сеть", "выкл."))),

            Planned("e2e-cache-check",
                "E2E API · Cache check",
                "Повторный прогон Dashboard-последовательности при включённом IMemoryCache → 0 запросов БД на 2-й прогон.",
                C(("Прогоны", "2"), ("Цель 2-го прогона", "0 запросов БД"), ("IMemoryCache", "вкл."))),

            Planned("e2e-bot-wizard-flow",
                "E2E Bot · Wizard flow",
                "/start → /place_order (4 шага) → /get_orders → /get_profile.",
                C(("Трейдеры", "1"), ("Токены", "50"), ("Команды", "7"))),

            Planned("e2e-bot-admin-flow",
                "E2E Bot · Admin flow",
                "/admin_bots_activity → /admin_stats → /admin_get_ids.",
                C(("Трейдеры", "1 (не-бот)"), ("MM-боты", "10"), ("Команды", "3"))),

            Planned("e2e-telegram-bot-level",
                "E2E Bot · TelegramBot level",
                "TelegramBot с фейковым IMessageSender: несколько команд подряд; замер от Update до ответа (без сети).",
                C(("Команды", "несколько подряд"), ("Сеть", "выкл. (фейковый IMessageSender)"))),
        };
    }

    private static ScenarioDefinition Service(string id, string title, string description, IReadOnlyDictionary<string, string> conditions)
        => new(id, title, "Сервис", description, true, conditions);

    private static ScenarioDefinition Planned(string id, string title, string description, IReadOnlyDictionary<string, string> conditions)
        => new(id, title, "E2E", description, false, conditions);

    private static IReadOnlyDictionary<string, string> C(params (string Key, string Value)[] pairs)
    {
        var result = new Dictionary<string, string>(pairs.Length);
        foreach (var (key, value) in pairs)
            result[key] = value;
        return result;
    }
}
