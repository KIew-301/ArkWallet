## Summary

- **29 commits** on `fix/wizard-command` (from main)
- **391 tests passing** (13 new)
- **44 files changed**

### What was done

**New commands:**
- `/get_order_book [symbol] [buy] [sell]` — quick variant (skip wizard steps)
- `/get_order_book` — 3-step wizard (select token → buy count → sell count → display)
- `/get_price_history` — 3-step wizard (select token → timeframe → limit)
- `/admin_update_token_media` — admin command for updating token icon/image URLs
- Refresh button on order book result — updates message in-place without chat clutter

**OrderBook service:**
- `IOrderBookService` + `OrderBookService` — fetches active orders, sorts Bids/Asks, computes spread
- `OrderBookServiceTest` — 12 unit tests (empty book, invalid symbol, edge cases)

**Wizard improvements:**
- `StepResult.Buttons` — pass inline buttons on wizard completion
- `ContinueCommand` propagates buttons to TelegramBot for final message
- `ButtonDecorator` extended for `/get_order_book` token selection
- Admin commands renamed to kebab-case (`/admin_add_balance_to_user`, `/admin_update_token_media`)

**Bugfixes:**
- `fix(Telegram): handle message is not modified` — catches Telegram `ApiRequestException` on refresh when data unchanged, shows toast instead of error
- `fix(Wizard): execute OneStep handlers in ContinueCommand flow`
- `fix(Presentation): check trader existence before showing name prompt in /start`
- `fix(MarketMaker): continue registration when trader already exists`

**MarketMaker:**
- `MarketMakerOrchestrator` ensures buyer+seller bots on all active tokens
- Dynamic token list instead of hardcoded symbol

**Other:**
- Price sell suggestions fixed + Active status filter
- CancelAllOrders: balance and token restoration tests
- Wizard layer no longer accesses DB directly
- All config via env vars (UserSecrets removed)

### This PR fixes (7 commits)

| # | Commit | Fix |
|---|--------|-----|
| 1 | `fix(encoding)` | Convert all cp1251 .cs files to UTF-8 with BOM — CI tests now pass |
| 2 | `test(TokenMediaUpdateService)` | 5 tests for validation and success paths |
| 3 | `test(TraderQueryService)` | 3 tests for profile queries including null username |
| 4 | `test(OrderCancellationService)` | 3 tests for `HasActiveOrdersAsync` |
| 5 | `test(TokenQueryService)` | 2 tests for `GetTokenInfoAsync` |
| 6 | `refactor(WizardHandlers)` | Extract duplicated token/int validation into helpers, deduplicate order book refresh |
| 7 | `refactor(WizardConfigurationPrivate)` | Consolidate duplicate admin step definitions |

**Coverage:** 58.6% → expected ≥80% (new tests cover all uncovered new services)
**Duplication:** 5.6% → expected <3% (extracted helpers, consolidated admin steps)
