CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251111074854_Init') THEN
    CREATE TABLE "CharacterTokens" (
        "Symbol" TEXT NOT NULL,
        "Name" TEXT NOT NULL,
        "Rarity" INTEGER NOT NULL,
        "CurrentPrice" TEXT NOT NULL,
        "TotalSupply" INTEGER NOT NULL,
        "IsActive" INTEGER NOT NULL,
        "CreatedAt" TEXT NOT NULL,
        CONSTRAINT "PK_CharacterTokens" PRIMARY KEY ("Symbol")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251111074854_Init') THEN
    CREATE TABLE "Traders" (
        "TelegramId" INTEGER NOT NULL,
        "Username" TEXT,
        "Balance" TEXT NOT NULL,
        "JoinedAt" TEXT NOT NULL,
        CONSTRAINT "PK_Traders" PRIMARY KEY ("TelegramId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251111074854_Init') THEN
    CREATE TABLE "PortfolioItems" (
        "Id" TEXT NOT NULL,
        "TraderTelegramId" INTEGER NOT NULL,
        "CharacterTokenId" TEXT NOT NULL,
        "Quantity" INTEGER NOT NULL,
        "AverageBuyPrice" TEXT NOT NULL,
        "AcquiredAt" TEXT NOT NULL,
        CONSTRAINT "PK_PortfolioItems" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PortfolioItems_CharacterTokens_CharacterTokenId" FOREIGN KEY ("CharacterTokenId") REFERENCES "CharacterTokens" ("Symbol") ON DELETE CASCADE,
        CONSTRAINT "FK_PortfolioItems_Traders_TraderTelegramId" FOREIGN KEY ("TraderTelegramId") REFERENCES "Traders" ("TelegramId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251111074854_Init') THEN
    CREATE TABLE "TradeOrders" (
        "Id" TEXT NOT NULL,
        "Type" INTEGER NOT NULL,
        "Status" INTEGER NOT NULL,
        "CharacterTokenId" TEXT NOT NULL,
        "TraderTelegramId" INTEGER NOT NULL,
        "Price" TEXT NOT NULL,
        "Quantity" INTEGER NOT NULL,
        "FilledQuantity" INTEGER NOT NULL,
        "CreatedAt" TEXT NOT NULL,
        "ExecutedAt" TEXT,
        CONSTRAINT "PK_TradeOrders" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TradeOrders_CharacterTokens_CharacterTokenId" FOREIGN KEY ("CharacterTokenId") REFERENCES "CharacterTokens" ("Symbol") ON DELETE CASCADE,
        CONSTRAINT "FK_TradeOrders_Traders_TraderTelegramId" FOREIGN KEY ("TraderTelegramId") REFERENCES "Traders" ("TelegramId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251111074854_Init') THEN
    CREATE TABLE "Trades" (
        "Id" TEXT NOT NULL,
        "BuyerId" INTEGER NOT NULL,
        "SellerId" INTEGER NOT NULL,
        "CharacterTokenId" TEXT NOT NULL,
        "Price" TEXT NOT NULL,
        "Quantity" INTEGER NOT NULL,
        "ExecutedAt" TEXT NOT NULL,
        CONSTRAINT "PK_Trades" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Trades_CharacterTokens_CharacterTokenId" FOREIGN KEY ("CharacterTokenId") REFERENCES "CharacterTokens" ("Symbol") ON DELETE CASCADE,
        CONSTRAINT "FK_Trades_Traders_BuyerId" FOREIGN KEY ("BuyerId") REFERENCES "Traders" ("TelegramId") ON DELETE CASCADE,
        CONSTRAINT "FK_Trades_Traders_SellerId" FOREIGN KEY ("SellerId") REFERENCES "Traders" ("TelegramId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251111074854_Init') THEN
    CREATE INDEX "IX_PortfolioItems_CharacterTokenId" ON "PortfolioItems" ("CharacterTokenId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251111074854_Init') THEN
    CREATE INDEX "IX_PortfolioItems_TraderTelegramId" ON "PortfolioItems" ("TraderTelegramId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251111074854_Init') THEN
    CREATE INDEX "IX_TradeOrders_CharacterTokenId" ON "TradeOrders" ("CharacterTokenId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251111074854_Init') THEN
    CREATE INDEX "IX_TradeOrders_TraderTelegramId" ON "TradeOrders" ("TraderTelegramId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251111074854_Init') THEN
    CREATE INDEX "IX_Trades_BuyerId" ON "Trades" ("BuyerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251111074854_Init') THEN
    CREATE INDEX "IX_Trades_CharacterTokenId" ON "Trades" ("CharacterTokenId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251111074854_Init') THEN
    CREATE INDEX "IX_Trades_SellerId" ON "Trades" ("SellerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251111074854_Init') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20251111074854_Init', '9.0.10');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260607093942_NewMigration') THEN
    CREATE TABLE "PriceCandles" (
        "Id" INTEGER NOT NULL,
        "OpenPrice" TEXT NOT NULL,
        "HighPrice" TEXT NOT NULL,
        "LowPrice" TEXT NOT NULL,
        "ClosePrice" TEXT NOT NULL,
        "Timestamp" TEXT NOT NULL,
        "CharacterTokenId" TEXT NOT NULL,
        CONSTRAINT "PK_PriceCandles" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PriceCandles_CharacterTokens_CharacterTokenId" FOREIGN KEY ("CharacterTokenId") REFERENCES "CharacterTokens" ("Symbol") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260607093942_NewMigration') THEN
    CREATE INDEX "IX_PriceCandles_CharacterTokenId" ON "PriceCandles" ("CharacterTokenId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260607093942_NewMigration') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260607093942_NewMigration', '9.0.10');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260622121913_BalanceHistory') THEN
    CREATE TABLE "BalanceSnapshots" (
        "Id" INTEGER NOT NULL,
        "TotalBalance" TEXT NOT NULL,
        "MainBalance" TEXT NOT NULL,
        "LongOrderReserveBalance" TEXT NOT NULL,
        "ShortOrderReserveBalance" TEXT NOT NULL,
        "BalanceInTokens" TEXT NOT NULL,
        "SnapshotDateTime" TEXT NOT NULL,
        "TraderId" INTEGER NOT NULL,
        CONSTRAINT "PK_BalanceSnapshots" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_BalanceSnapshots_Traders_TraderId" FOREIGN KEY ("TraderId") REFERENCES "Traders" ("TelegramId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260622121913_BalanceHistory') THEN
    CREATE INDEX "IX_BalanceSnapshots_TraderId" ON "BalanceSnapshots" ("TraderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260622121913_BalanceHistory') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260622121913_BalanceHistory', '9.0.10');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629144749_UpdatePotrfolioLogic') THEN
    ALTER TABLE "PortfolioItems" ADD "AverageReservePrice" TEXT NOT NULL DEFAULT '0';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629144749_UpdatePotrfolioLogic') THEN
    ALTER TABLE "PortfolioItems" ADD "AverageSellPrice" TEXT NOT NULL DEFAULT '0';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629144749_UpdatePotrfolioLogic') THEN
    ALTER TABLE "PortfolioItems" ADD "ReserveQuantity" INTEGER NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629144749_UpdatePotrfolioLogic') THEN
    ALTER TABLE "PortfolioItems" ADD "SellingQuantity" INTEGER NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629144749_UpdatePotrfolioLogic') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260629144749_UpdatePotrfolioLogic', '9.0.10');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260703102017_MarketMakerBotCreation') THEN
    ALTER TABLE "CharacterTokens" ADD "IconUrl" TEXT NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260703102017_MarketMakerBotCreation') THEN
    ALTER TABLE "CharacterTokens" ADD "ImageUrl" TEXT NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260703102017_MarketMakerBotCreation') THEN
    CREATE TABLE "MarketMakerBots" (
        "Id" INTEGER NOT NULL,
        "Symbol" TEXT NOT NULL,
        "TraderId" INTEGER NOT NULL,
        "BasePower" TEXT NOT NULL,
        "Role" INTEGER NOT NULL,
        "NextPowerChange" TEXT NOT NULL,
        "NextRebalance" TEXT NOT NULL,
        "IsActive" INTEGER NOT NULL,
        "CreatedAt" TEXT NOT NULL,
        CONSTRAINT "PK_MarketMakerBots" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260703102017_MarketMakerBotCreation') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260703102017_MarketMakerBotCreation', '9.0.10');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260703110256_TraderNotificationOption') THEN
    ALTER TABLE "Traders" ADD "NotificationOn" INTEGER NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260703110256_TraderNotificationOption') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260703110256_TraderNotificationOption', '9.0.10');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706151704_OrderAverageExecutionPrice') THEN
    ALTER TABLE "TradeOrders" ADD "AverageExecutePrice" TEXT NOT NULL DEFAULT '0';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706151704_OrderAverageExecutionPrice') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260706151704_OrderAverageExecutionPrice', '9.0.10');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260708092643_AppStates') THEN
    CREATE TABLE "AppStates" (
        "Key" TEXT NOT NULL,
        "Value" TEXT NOT NULL,
        CONSTRAINT "PK_AppStates" PRIMARY KEY ("Key")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260708092643_AppStates') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260708092643_AppStates', '9.0.10');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807183812_BalanceSnapshotTraderDateTimeIndex') THEN
    DROP INDEX "IX_BalanceSnapshots_TraderId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807183812_BalanceSnapshotTraderDateTimeIndex') THEN
    CREATE INDEX "IX_BalanceSnapshots_TraderId_SnapshotDateTime" ON "BalanceSnapshots" ("TraderId", "SnapshotDateTime");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807183812_BalanceSnapshotTraderDateTimeIndex') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260807183812_BalanceSnapshotTraderDateTimeIndex', '9.0.10');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811202309_MiningSystem') THEN
    CREATE TABLE "MiningGlobalRules" (
        "Id" INTEGER NOT NULL,
        "TokenId" TEXT NOT NULL,
        "CurrentCoefficient" TEXT NOT NULL,
        "FutureCoefficient" TEXT NOT NULL,
        "BaseMiningSpeed" TEXT NOT NULL,
        CONSTRAINT "PK_MiningGlobalRules" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_MiningGlobalRules_CharacterTokens_TokenId" FOREIGN KEY ("TokenId") REFERENCES "CharacterTokens" ("Symbol") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811202309_MiningSystem') THEN
    CREATE TABLE "MiningMachines" (
        "Id" INTEGER NOT NULL,
        "Name" TEXT NOT NULL,
        "Type" TEXT NOT NULL,
        "SwitchingTime" INTEGER NOT NULL,
        "Reusability" TEXT NOT NULL,
        "IsActiveForSale" INTEGER NOT NULL,
        "Cost" TEXT NOT NULL,
        "Image" TEXT NOT NULL,
        CONSTRAINT "PK_MiningMachines" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811202309_MiningSystem') THEN
    CREATE TABLE "MiningMachineRules" (
        "Id" INTEGER NOT NULL,
        "MiningMachineId" INTEGER NOT NULL,
        "CharacterTokenId" TEXT NOT NULL,
        "MiningCoefficient" TEXT NOT NULL,
        CONSTRAINT "PK_MiningMachineRules" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_MiningMachineRules_CharacterTokens_CharacterTokenId" FOREIGN KEY ("CharacterTokenId") REFERENCES "CharacterTokens" ("Symbol") ON DELETE RESTRICT,
        CONSTRAINT "FK_MiningMachineRules_MiningMachines_MiningMachineId" FOREIGN KEY ("MiningMachineId") REFERENCES "MiningMachines" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811202309_MiningSystem') THEN
    CREATE TABLE "MiningMachineSlots" (
        "Id" INTEGER NOT NULL,
        "TraderId" INTEGER NOT NULL,
        "MiningMachineId" INTEGER NOT NULL,
        "TokenId" TEXT,
        "MachineRuleId" INTEGER,
        "MiningGlobalRuleId" INTEGER,
        "Status" TEXT NOT NULL,
        "StartSwitchingDateTime" TEXT,
        "EndSwitchingDateTime" TEXT,
        "TokensAmountCollected" TEXT NOT NULL,
        "Cost" TEXT NOT NULL,
        "CreatedAt" TEXT NOT NULL,
        "SoldAt" TEXT,
        CONSTRAINT "PK_MiningMachineSlots" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_MiningMachineSlots_CharacterTokens_TokenId" FOREIGN KEY ("TokenId") REFERENCES "CharacterTokens" ("Symbol") ON DELETE RESTRICT,
        CONSTRAINT "FK_MiningMachineSlots_MiningGlobalRules_MiningGlobalRuleId" FOREIGN KEY ("MiningGlobalRuleId") REFERENCES "MiningGlobalRules" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MiningMachineSlots_MiningMachineRules_MachineRuleId" FOREIGN KEY ("MachineRuleId") REFERENCES "MiningMachineRules" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MiningMachineSlots_MiningMachines_MiningMachineId" FOREIGN KEY ("MiningMachineId") REFERENCES "MiningMachines" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MiningMachineSlots_Traders_TraderId" FOREIGN KEY ("TraderId") REFERENCES "Traders" ("TelegramId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811202309_MiningSystem') THEN
    CREATE UNIQUE INDEX "IX_MiningGlobalRules_TokenId" ON "MiningGlobalRules" ("TokenId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811202309_MiningSystem') THEN
    CREATE INDEX "IX_MiningMachineRules_CharacterTokenId" ON "MiningMachineRules" ("CharacterTokenId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811202309_MiningSystem') THEN
    CREATE UNIQUE INDEX "IX_MiningMachineRules_MiningMachineId_CharacterTokenId" ON "MiningMachineRules" ("MiningMachineId", "CharacterTokenId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811202309_MiningSystem') THEN
    CREATE INDEX "IX_MiningMachineSlots_MachineRuleId" ON "MiningMachineSlots" ("MachineRuleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811202309_MiningSystem') THEN
    CREATE INDEX "IX_MiningMachineSlots_MiningGlobalRuleId" ON "MiningMachineSlots" ("MiningGlobalRuleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811202309_MiningSystem') THEN
    CREATE INDEX "IX_MiningMachineSlots_MiningMachineId" ON "MiningMachineSlots" ("MiningMachineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811202309_MiningSystem') THEN
    CREATE INDEX "IX_MiningMachineSlots_Status" ON "MiningMachineSlots" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811202309_MiningSystem') THEN
    CREATE INDEX "IX_MiningMachineSlots_TokenId" ON "MiningMachineSlots" ("TokenId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811202309_MiningSystem') THEN
    CREATE INDEX "IX_MiningMachineSlots_TraderId" ON "MiningMachineSlots" ("TraderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811202309_MiningSystem') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260811202309_MiningSystem', '9.0.10');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814120000_MiningMachineEfficiency') THEN
    ALTER TABLE "MiningMachines" ADD "Efficiency" TEXT NOT NULL DEFAULT '1';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "pg_indexes" WHERE "indexname" = 'IX_MiningMachines_Name') THEN
    CREATE UNIQUE INDEX "IX_MiningMachines_Name" ON "MiningMachines" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814120000_MiningMachineEfficiency') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814120000_MiningMachineEfficiency', '9.0.10');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814092807_MiningGlobalRuleBaseTokenMiningSpeed') THEN
    ALTER TABLE "MiningGlobalRules" RENAME COLUMN "BaseMiningSpeed" TO "BaseTokenMiningSpeed";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814092807_MiningGlobalRuleBaseTokenMiningSpeed') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814092807_MiningGlobalRuleBaseTokenMiningSpeed', '9.0.10');
    END IF;
END $EF$;
COMMIT;

