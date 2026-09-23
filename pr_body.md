## Summary

Adds a paid **subscription system** for ArkWallet: tiered limits, period-based pricing (week/month/year) with upgrade bonuses, a two-stage YooKassa payment flow, background payment-confirmation and expiry workers, management wizard commands, and a SonarCloud cleanup across the analyzed codebase.

**Subscriptions:**
- EF entities `Subscription`, `SubscriptionPayment`, `SubscriptionPurchaseHistory`; `Trader` gets `SubscriptionId`/`SubscriptionExpiresAtUtc`; `SubscriptionId`/`SubscriptionExpiresAtUtc` columns on `Trader`.
- Base tier (Level 1, perpetual, 5 orders / 5 mining machines) seeded at startup.
- Paid levels with limits on active orders (all tokens) and mining machines; bots (BotFilter) are not limited.
- Purchase history with `TransactionId`; recorded also on expiry downgrade.

**Pricing & periods:**
- Weekly / monthly / yearly prices; period-based purchase.
- Upgrade bonus: remaining time carried over plus bonus for higher level; same-level purchase extends duration.
- `PowerDeviationCoeff` column added to the EF snapshot (was missing while present in the model).

**Payment (two-stage):**
- `IPaymentIntegrationService` with `CreatePaymentAsync`/`GetPaymentStatusAsync`.
- `YooKassaPaymentIntegrationService`: requires confirmation until status `succeeded`; returns `ConfirmationUrl`.
- `InstantSuccessPaymentService` (stub) for tests/dev.
- `SubscriptionPurchaseService.PurchaseAsync`: creates a pending `SubscriptionPayment` with `ConfirmationUrl` when confirmation is required, otherwise activates immediately; the payment link is returned to the bot.

**Workers:**
- `PaymentConfirmationWorker` (BackgroundService): polls pending payments every 15 s, activates subscription on `succeeded`, marks `canceled` on `canceled`/`expired`; on a transient YooKassa error returns `"error"` so the payment is retried, never canceled.
- `SubscriptionExpiryWorker`: downgrades expired subscriptions to the base tier and records purchase history.

**Wizard / Telegram:**
- `/subscriptions`, `/buy_subscription <id>`, `/admin_create_subscription {json}`, `/admin_set_trader_subscription {json}`.

**SonarCloud cleanup (`chore`):**
- CS1591 XML doc comments added across entities, value objects, services, DTOs and commands.
- Nullability (CS8618/8602/8604/8621): `= null!;`, `?? string.Empty`, null-guards, `.Where(k => k is not null)`.
- S2325/CA1822: static methods (`FixedGridEngine` grid methods + call sites, `BuildAuthController`).
- S3776 cognitive complexity reduced (`BalanceSnapshotService`).
- S6960: `OrdersController` split into `OrdersQueryController`/`OrdersCreationController`/`OrdersCancellationController`, routes preserved 1:1.
- CS1587: record param XML moved to `<param>` tags (`TelegramUserData`/`TelegramInitData`).
- Dockerfile merged `RUN` layers (docker:S7031); `setup-server.sh` `[[ ]]`→`[ ]` (shell:S7688).

## Changes

**Core/<SubscriptionContext> (Domain/EF):**
- `Core/SubscriptionContext/Domain/Entities/Subscription.cs`, `Core/SubscriptionContext/Domain/Entities/SubscriptionPayment.cs`, `Core/SubscriptionContext/Domain/Entities/SubscriptionPurchaseHistory.cs` — new aggregates.
- `Core/SubscriptionContext/Domain/ValueObjects/SubscriptionPeriod.cs` — period enum + duration/minutes/display name.

**Core/SubscriptionContext (Application):**
- `SubscriptionActivationService` — activation logic: expiry resolution (extend/upgrade bonus), history record, `TraderSubscriptionChangedEvent`.
- `SubscriptionPurchaseService` — two-stage purchase; pending payment + `ConfirmationUrl` when `RequiresConfirmation`.
- `SubscriptionExpiryService` — downgrade to base tier.
- `SubscriptionQueryService`, `SubscriptionPurchaseHistoryQueryService` — queries.
- Domain events `TraderSubscriptionChangedEvent` (Application/Events).

**Infrastructure:**
- `Infrastructure/Data/Subscription.cs`, `SubscriptionPayment.cs`, `SubscriptionPurchaseHistory.cs`, `Trader.cs` (+ columns); EF migration.
- `Infrastructure/Payment/YooKassaIntegration/YooKassaPaymentIntegrationService.cs` — YooKassa API, `SubscriptionPayment` → status mapping.
- `Infrastructure/Payment/Instant/InstantSuccessPaymentService.cs` — instant stub.
- `Infrastructure/Workers/PaymentConfirmationWorker.cs` — status polling, activation/cancel (15 s).
- `Infrastructure/Workers/SubscriptionExpiryWorker.cs` — expiry handling.

**Presentation / API:**
- `SubscriptionsController`, `SubscriptionPurchasesController` — subscriptions API.
- `OrdersQueryController`/`OrdersCreationController`/`OrdersCancellationController` — split from `OrdersController` (S6960).
- `StartProgram.cs` — DI: payment provider switch (`Payment:Provider` = `YooKassa`|`Instant`), services, both hosted workers.
- Wizard: `/subscriptions`, `/buy_subscription`, admin subscription commands.

## Verification

- `dotnet build` — 0 errors.
- `dotnet test` — 1089 passed, 0 failed, 8 skipped.
- SonarCloud Quality Gate (main): PASSED (coverage 97.8%, duplicated 0%).
