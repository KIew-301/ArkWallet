# 🏦 ArkWallet — High-Load Token Trading Simulator

<div align="center">

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?style=for-the-badge&logo=postgresql&logoColor=white)
![RabbitMQ](https://img.shields.io/badge/RabbitMQ-FF6600?style=for-the-badge&logo=rabbitmq&logoColor=white)
![Prometheus](https://img.shields.io/badge/Prometheus-E6522C?style=for-the-badge&logo=prometheus&logoColor=white)
![Grafana](https://img.shields.io/badge/Grafana-F46800?style=for-the-badge&logo=grafana&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white)
![GitHub Actions](https://img.shields.io/badge/GitHub_Actions-2088FF?style=for-the-badge&logo=githubactions&logoColor=white)
![Telegram](https://img.shields.io/badge/Telegram_Bot-26A5E4?style=for-the-badge&logo=telegram&logoColor=white)
![Swagger](https://img.shields.io/badge/Swagger-85EA2D?style=for-the-badge&logo=swagger&logoColor=black)
![SonarCloud](https://img.shields.io/badge/SonarCloud-F3702A?style=for-the-badge&logo=sonarcloud&logoColor=white)
![License](https://img.shields.io/badge/License-PolyForm%20NC-blue?style=for-the-badge&logo=shield&logoColor=white)

</div>

---

## 📋 Table of Contents

- [🧠 Overview](#-overview)
- [🚀 Core Capabilities](#-core-capabilities)
- [🧱 Architecture](#-architecture)
- [⚙️ Trading Engine](#️-trading-engine)
- [🏗️ Domain Model](#️-domain-model)
- [🔧 Service Layer](#-service-layer)
- [📡 API Endpoints](#-api-endpoints)
- [📈 Observability](#-observability)
- [⚡ Performance Testing](#-performance-testing)
- [🛠 Technology Stack](#-technology-stack)
- [🤝 Connect](#-connect)
- [📄 License](#-license)

---

## 🧠 Overview

**ArkWallet** is a **high-load token trading platform** built as a mini-app for Telegram. It simulates a full-featured trading ecosystem with real-time order matching, balance snapshots, candle aggregation, an automated market-making system, an in-app mail inbox with rewards, a gift system, global goals, and built-in performance & observability tooling.

> **Key Objective:** Demonstrate production-ready architecture, clean code principles, and deep understanding of distributed systems — not just a working prototype.

---

## 🚀 Core Capabilities

| Feature | Description |
|---------|-------------|
| **💱 Token Trading** | Place buy/sell orders via REST API or Telegram Bot, incl. batch order creation |
| **⚡ Order Matching Engine** | Custom engine with partial fills, average execution price, and in-memory transactionality |
| **📊 Balance Snapshots** | Periodic full-state balance snapshots with historical analytics (batched for multiple traders) |
| **🕯️ Candle Aggregation** | Flexible timeframe aggregation (1m, 5m, 15m, 1h, etc.) |
| **🤖 Market Maker Bots** | Automated liquidity providers with dynamic pricing grids, batched grid order placement |
| **📬 In-App Mail** | Unified inbox (`/open_mail`): notification, reward and gift mail with collectable rewards; admin broadcast via `/admin_send_mail` |
| **🎁 Gift System** | Send tokens from your portfolio to another user (`/send_gift`), collect pending gifts; per-recipient cooldown and eligibility rules |
| **🎯 Global Goals** | Balance-based global goals with achievement validation and reward mail |
| **📨 Notification System** | RabbitMQ-based event bus with Telegram notifications |
| **🌐 Resilient Telegram Bot** | Webhook delivery with retry/backoff, per-chat parallel processing, and per-endpoint rate limiting (429 with `Retry-After`) |
| **🔐 Concurrency Control** | Pessimistic row locking (`SELECT ... FOR UPDATE`) and transaction wrapper to serialize trades and price updates |
| **📈 Observability** | Health checks, OpenTelemetry metrics, Prometheus scraping, Grafana dashboards, `/admin_metrics` bot command |
| **⚡ Performance Gates** | Dedicated perf-testing project with EF query counters, budget gates, and HTML reports |

---

## 🧱 Architecture

The project follows **Clean Architecture** with **Domain-Driven Design** principles, strictly separated into four layers. Code is organized into `ArkWallet/Core/<Context>/{Domain,Application}` folders (`TradingContext`, `General`, `GiftContext`, `PortfolioContext`, `MailContext`, `MiningContext`, `ShoppingContext`); the EF persistence layer lives in `Infrastructure/Data` as anemic `EntityData` records (no behavior — only properties, `Create`/`Update` factories) validated by architectural `ArchitectureLayerRules` (Rule 18). Business logic resides entirely in the domain aggregates.

<div align="center">
<br>
<table>
<tr>
<td align="center" width="25%">
<strong>🎨 Presentation</strong><br>
REST API (Swagger)<br>
Telegram Bot
</td>
<td align="center" width="25%">
<strong>🔧 Infrastructure</strong><br>
EF Core<br>
RabbitMQ<br>
Background Jobs
</td>
<td align="center" width="25%">
<strong>⚙️ Application</strong><br>
Use Cases<br>
DTOs<br>
Service Contracts
</td>
<td align="center" width="25%">
<strong>💎 Domain</strong><br>
Entities<br>
Value Objects<br>
Engines
</td>
</tr>
</table>
<br>
</div>

### Design Principles Applied

- ✅ **SOLID** — Single Responsibility, Dependency Injection
- ✅ **Domain-Driven Design** — Rich domain model, value objects, invariants; EF `Infrastructure.Data` records are anemic (Rule 18) — all behavior lives in domain aggregates
- ✅ **Result Pattern** — Explicit error handling without exceptions
- ✅ **Unit Testing** — xUnit + Moq + Coverlet (990+ tests), Testcontainers for PostgreSQL race scenarios
- ✅ **Code Quality** — SonarCloud integration (coverage, duplications, hotspots)

---

## ⚙️ Trading Engine

At the core of the platform lies a **custom order matching engine** that processes trades atomically in memory before persisting to the database.

### Processing Pipeline

| Step | Action |
|------|--------|
| **1️⃣ Validation** | Verify price, quantity, and trader funds (group validation in a single query for batches) |
| **2️⃣ Lock Rows** | Pessimistic trader/token locks on PostgreSQL to serialize concurrent updates |
| **3️⃣ Reserve Funds** | Lock balance (buy) or tokens (sell) |
| **4️⃣ Load Order Book** | Fetch active opposite-side orders from DB |
| **5️⃣ Find Matches** | Sort by best price (ascending for buys, descending for sells) |
| **6️⃣ Execute Trades** | Calculate trade volume, update balances/portfolios, track partial fills |
| **7️⃣ Return Result** | Atomic `TradingContext` with all changes, staged and saved in a single DB transaction |

### Key Characteristics

| Property | Implementation |
|----------|---------------|
| **🔒 Encapsulation** | Engine lives in Domain layer, zero external dependencies |
| **🔄 Partial Fills** | Orders stay active with remaining quantity |
| **📊 Average Price** | Weighted average calculated across all fills |
| **⚛️ Atomicity** | All changes computed in-memory, saved in a single DB transaction (optionally batched via `CreateOrdersAsync`) |

### Example Execution

**Input:**
- New order: **Buy 10 ZZZ @ 100**
- Order Book:
  - Sell 3 ZZZ @ 90
  - Sell 4 ZZZ @ 95
  - Sell 5 ZZZ @ 100

**Execution:**

| Trade | Qty | Price | Total |
|-------|-----|-------|-------|
| 1 | 3 | 90 | 270 |
| 2 | 4 | 95 | 380 |
| 3 | 3 | 100 | 300 |
| **Total** | **10** | | **950** |

**Result:** Average execution price = 95, order fully filled, token price updated to 100.

---

## 🏗️ Domain Model

The domain layer contains a rich set of entities with encapsulated business logic and enforced invariants.

### Entity Catalog

| Entity | Responsibility | Key Invariants |
|--------|---------------|----------------|
| **👤 Trader** | User account with balance management | Balance ≥ 0, unique Telegram ID |
| **💾 BalanceSnapshot** | Immutable full-state balance record | Created via factory method only |
| **🎲 CharacterToken** | Tradable token with price and supply | Price ≥ 0, symbol uniqueness |
| **📦 PortfolioItem** | Token holdings with reserve tracking | Quantity + Reserve + Selling = total |
| **📝 TradeOrder** | Buy/sell order with status lifecycle | Price/quantity > 0, cancel only own active orders |
| **🔄 Trade** | Executed exchange record | Always links buyer and seller |
| **🕯️ PriceCandle** | OHLC price data per timeframe | Open ≤ High, Low ≤ Close |
| **🤖 MarketMakerBot** | Automated liquidity provider | Dynamic power with randomized intervals |
| **📬 Message** | Mail inbox entity with status transitions | Status: unread → read / accepted; reward collected on accept |
| **🎁 Gift** | Token transfer between users | Self-gift forbidden; 8h cooldown per recipient; only tokens ≤ 1000 stg with balance ≥ 1 |
| **🎯 GlobalGoal** | Balance-based achievement definition | Target balance triggers reward mail event |


### Business Invariants

| Entity | Invariant |
|--------|-----------|
| **Trader** | Balance cannot be negative |
| **TradeOrder** | Price and quantity must be > 0 |
| **TradeOrder** | Only active orders can be cancelled |
| **TradeOrder** | Only order owner can cancel |
| **PortfolioItem** | Cannot reserve more than available |
| **PortfolioItem** | Cannot sell more than reserved |
| **CharacterToken** | Price cannot be negative |
| **BalanceSnapshot** | Immutable after creation |
| **Message** | Must have reward to accept |
| **Gift** | Cannot self-gift; max 1 gift per recipient per 8 hours |

---

## 🔧 Service Layer

All services follow the **Single Responsibility Principle** and are organized by domain area.

### Token Services

| Service | Purpose |
|---------|---------|
| `ITokenCreationService` | Create new tokens with uniqueness validation |
| `ITokenQueryService` | List all active tokens |
| `ITokenPriceCandleQueryService` | Fetch historical price candles |
| `ITokenPriceCandleUpdateService` | Update candles on price change (new candle per minute) |
| `ITokenPriceChangesCalculationService` | Calculate absolute and percentage price changes |
| `ICandleAggregatorService` | Aggregate 1m candles into higher timeframes |

### Order Services

| Service | Purpose |
|---------|---------|
| `IOrderCreationService` | Create and process orders through trading engine (single + batched `CreateOrdersAsync`) |
| `IOrderCreationFullValidationService` | Full validation pipeline for order creation |
| `IOrderValidationService` | Individual parameter validation (price, quantity, direction) |
| `IOrderCancellationService` | Cancel single or all active orders with fund return (bulk status update) |
| `IOrderQueryService` | Query orders with status filtering (untracked projection) |

### Trader Services

| Service | Purpose |
|---------|---------|
| `ITraderRegistrationService` | Register new traders |
| `ITraderBalanceUpdatingService` | Top up trader balance |
| `IBalanceSnapshotService` | Create full balance snapshots |
| `IBalanceSavingService` | Persist snapshots to database |
| `IBalanceChangesCalculationService` | Calculate balance changes over periods |

### Portfolio Services

| Service | Purpose |
|---------|---------|
| `IPortfolioQueryService` | Query token balances with profit/loss calculation |
| `IPortfolioUpdatingService` | Create or update portfolio positions |

### Trade Services

| Service | Purpose |
|---------|---------|
| `ITradeQueryService` | Query trade history with profit/loss per trade |

### Market Maker Services

| Service | Purpose |
|---------|---------|
| `IMarketMakerBotRegistrationService` | Register bots as traders |
| `IMarketMakerOrderService` | Execute market orders based on bot role and power |

### Orchestration Services

| Service | Purpose |
|---------|---------|
| `IMarketMakerOrchestrator` | Manage bot lifecycle (registration, balance, grids, orders) |
| `ICandleOrchestrator` | Fetch and aggregate candles |
| `IMarketWallBlockerOrchestrator` | Detect and cancel anti-pattern orders (blocker bots) |

### Mail, Gift & Global Goal Services

| Service | Purpose |
|---------|---------|
| `IMailMessageService` / `IMailStatusUpdatingService` / `IMailQueryService` | Create, read and collect rewards from mail |
| `IGiftSendingService` / `IGiftReceivingService` / `IQueryGiftService` | Send, receive and list gifts |
| `TotalBalanceGlobalGoalCalculation` | Evaluate global-goal achievements and trigger reward mail |

### Suggestion Services

| Service | Purpose |
|---------|---------|
| `IPriceSuggestionService` | Generate price suggestions for orders |
| `IQuantitySuggestionService` | Generate quantity suggestions based on available funds |

### Telegram Decorators

| Service | Purpose |
|---------|---------|
| `IQuestionDecorator` | Enrich Wizard questions with trader context |
| `IButtonDecorator` | Dynamically generate context-aware buttons |

### Authentication Services

| Service | Purpose |
|---------|---------|
| `ITokenService` | Generate JWT tokens (7-day lifetime, HMAC-SHA256) |
| `ITraderAuthService` | Authenticate via Telegram WebApp InitData |

---

## 📡 API Endpoints

| Controller | Method | Endpoint | Description |
|-----------|--------|----------|-------------|
| **Auth** | POST | `/api/auth/login` | Telegram WebApp authentication |
| **Tokens** | GET | `/api/tokens/token` | List all active tokens |
| **Tokens** | GET | `/api/tokens/price-candle` | Get price candles with aggregation |
| **Orders** | GET | `/api/orders` | Get orders with status filtering |
| **Orders** | POST | `/api/orders` | Create new order |
| **Orders** | DELETE | `/api/orders/{orderId}` | Cancel specific order |
| **Orders** | DELETE | `/api/orders` | Cancel all active orders |
| **Portfolios** | GET | `/api/portfolios` | Get trader portfolio |
| **Traders** | GET | `/api/traders/balance` | Get balance with period changes |
| **Trades** | GET | `/api/trades` | Get trade history |
| **Health** | GET | `/health` | Liveness/readiness probe (DB check) for Docker healthcheck |
| **Metrics** | GET | `/metrics` | Prometheus-compatible OpenTelemetry metrics |
| **Bot** | POST | `/bot/webhook` | Telegram webhook endpoint (secret-token validated) |

> **Rate Limiting:** API endpoints are rate-limited per `(client IP, endpoint)` — 1 request/second per endpoint, global cap 3 requests/second per client; auth endpoints are stricter (1 per 5s). Bot commands are capped at 3 per second per chat. Over-limit responses return `429 Too Many Requests` with `Retry-After`.

---

## 📈 Observability

- **Health checks** — `/health` endpoint with a database probe (`DatabaseHealthCheck`), wired into the Docker healthcheck.
- **OpenTelemetry metrics** — ASP.NET Core instrumentation + Npgsql meters + custom `ArkWalletMetrics` counters/histograms:
  - `arkwallet_service_results_total` — Ok/Fail results per service
  - `arkwallet_lock_wait_seconds` — `SELECT ... FOR UPDATE` lock wait time
  - `arkwallet_commands_total` / `arkwallet_command_duration_seconds` — Telegram bot command usage
- **Prometheus** (`prom/prometheus:v2.53.0`) — scrapes the app at `http://app:5000/metrics` (config in `prometheus.yml`, 15s interval, Bearer auth), UI on port `9090`.
- **Grafana** (`grafana/grafana:11.1.0`) — runs on port `3000` via docker-compose. Prometheus datasource and the **ArkWallet Overview** dashboard (service results, lock wait, bot commands, HTTP, Npgsql) are provisioned automatically from `grafana/` (datasource + file-provider dashboards).
- **Admin bot** — `/admin_metrics` command exports the same snapshot in Telegram.
- **Protected metrics** — `/metrics` requires `Authorization: Bearer <Metrics__ApiKey>` (`MetricsApiKeyMiddleware`), so only services/admins with the key can read the endpoint.
- **Protected Grafana** — access only for admins: login `admin` / password = `METRICS_API_KEY` (same secret), anonymous access and sign-up disabled.
- **Deploy** — `METRICS_API_KEY` is injected into `.env` and rendered into `prometheus.yml` from GitHub Secrets during deploy.

### Telegram Integration

- **Webhook delivery** — updates arrive via `POST /bot/webhook` (validated by `X-Telegram-Bot-Api-Secret-Token`), processed off the request path; falls back to legacy `getUpdates` polling when no webhook URL is configured.
- **Resilient sends** — every outgoing Bot API call is wrapped in a retry policy (15s attempt timeout, 3 attempts, exponential backoff, honors `429 Retry-After`), swallowing benign "message is not modified".
- **Per-chat parallelism** — update handlers run concurrently per chat, so a hung API request no longer stalls the whole bot.
- **Anti-spam** — per-chat command rate cap (3/s) protects the `WizardEngine` state from bursts.

---

## ⚡ Performance Testing

The `ArkWallet.PerformanceTests` project (excluded from CI and the main solution) provides:

- **EF Core `IDbCommandInterceptor`** (`QueryCounter`) — counts queries, tracks execution time and slowest SQL.
- **`PerfScope.Step`** — measures `{name, ms, queries}` deltas around each code region.
- **Query-count gates** — per-service budgets for orders, snapshots, leaders, tokens, and market-maker ticks (#26-30), plus E2E API/bot gates (#31-36).
- **Repeat mode** — median reports over N runs, row/cache counters, query-regression detection.
- **HTML summary reports** — saved to `ArkWallet.PerformanceTests/Reports/` on every run.

---

## 🛠 Technology Stack

| Layer | Technology |
|-------|-----------|
| **Runtime** | .NET 9 |
| **Framework** | ASP.NET Core 9 |
| **Database** | PostgreSQL (Npgsql, production) / SQLite (local dev & tests) |
| **Message Broker** | RabbitMQ (RabbitMQ.Client 7.2.0) |
| **Observability** | OpenTelemetry + Prometheus (`/metrics`) + Grafana (docker-compose) |
| **Authentication** | JWT (Microsoft.AspNetCore.Authentication.JwtBearer 9) |
| **API Documentation** | Swagger / OpenAPI (Swashbuckle.AspNetCore 9.0.6) |
| **Telegram SDK** | Telegram.Bot 22.7.5 |
| **Testing** | xUnit + Moq + Coverlet + Testcontainers (PostgreSQL race tests) |
| **Performance Tests** | ArkWallet.PerformanceTests — EF QueryCounter, budget gates, HTML reports |
| **CI/CD** | GitHub Actions + SonarCloud code analysis + Docker Compose deployment |
| **Code Quality** | SonarCloud |
| **Architecture** | Clean Architecture + DDD + CQRS |

---

## 🤝 Connect

- **Telegram:** [@DominoDominion](https://t.me/DominoDominion)
- **GitHub:** [github.com/KIew-301](https://github.com/KIew-301)

---

## 📄 License

This project is licensed under the PolyForm Noncommercial License 1.0.0 — see the [LICENSE](LICENSE) file for details.

**Source is open for reading. Commercial use is prohibited.**

---
