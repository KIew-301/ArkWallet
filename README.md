# ArkWallet

A system for virtual trading with tokens linked to game characters.

## `Architecture`

- **Domain**: Entities & business logic
- **Application**: Services & use cases 
- **Infrastructure**: Data access
- **Presentation**: Telegram bot

## `Key Services`

### `Domain Services`
- **TradingEngine** - Order matching and trade execution

### `Presentation Services`  
- **Wizard** - Step-by-step user interface for command processing

### `Application Services`

#### `Token Services`
- **TokenCreationService** - Create new character tokens
- **TokenQueryService** - Get token information and prices

#### `Portfolio Services`
- **PortfolioQueryService** - Get portfolio balances and positions
- **PortfolioUpdatingService** - Update portfolio after trades

#### `Order Services`
- **OrderCreationService** - Create and process new orders
- **OrderCancelService** - Cancel active orders
- **OrderQueryService** - Get order information and history
- **OrderValidationService** - Validate order parameters

#### `Trader Services`
- **TraderRegistrationService** - Register new traders
- **TraderQueryService** - Get trader information
- **TraderBalanceUpdatingService** - Update trader balances

#### `Suggestion Services`
- **PriceSuggestionService** - Generate optimal price recommendations
- **QuantitySuggestionService** - Generate optimal quantity suggestions

## `Contacts`
- Telegram - https://t.me/linoQwW

## 📄 `License`
This project is licensed under the MIT License - see the [Licence.md](Licence.md) file for details.
