START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122224023_FinancialTransactions') THEN
    ALTER TABLE "CATEGORIES" ADD "UsedByTransactions" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122224023_FinancialTransactions') THEN
    ALTER TABLE "CATEGORIES" ADD CONSTRAINT "AK_CATEGORIES_Id_UserId" UNIQUE ("Id", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122224023_FinancialTransactions') THEN
    ALTER TABLE "ACCOUNTS" ADD CONSTRAINT "AK_ACCOUNTS_Id_UserId" UNIQUE ("Id", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122224023_FinancialTransactions') THEN
    CREATE TABLE "FINANCIAL_REQUESTS" (
        "UserId" text NOT NULL,
        "RequestId" uuid NOT NULL,
        "Hash" character varying(64) NOT NULL,
        "Response" jsonb NOT NULL,
        "Status" integer NOT NULL,
        CONSTRAINT "PK_FINANCIAL_REQUESTS" PRIMARY KEY ("UserId", "RequestId"),
        CONSTRAINT "FK_FINANCIAL_REQUESTS_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122224023_FinancialTransactions') THEN
    CREATE TABLE "TRANSACTIONS" (
        "Id" uuid NOT NULL,
        "UserId" text NOT NULL,
        "Kind" character varying(10) NOT NULL,
        "AccountId" uuid NOT NULL,
        "DestinationAccountId" uuid,
        "CategoryId" uuid,
        "Amount" numeric(18,2) NOT NULL,
        "Date" date NOT NULL,
        "Description" character varying(500) NOT NULL,
        "IsVoided" boolean NOT NULL,
        "Version" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_TRANSACTIONS" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_TRANSACTIONS_Id_UserId" UNIQUE ("Id", "UserId"),
        CONSTRAINT "CK_TRANSACTIONS_Amount" CHECK ("Amount" > 0),
        CONSTRAINT "CK_TRANSACTIONS_Shape" CHECK (("Kind" IN ('income','expense') AND "CategoryId" IS NOT NULL AND "DestinationAccountId" IS NULL) OR ("Kind" = 'transfer' AND "CategoryId" IS NULL AND "DestinationAccountId" IS NOT NULL AND "AccountId" <> "DestinationAccountId")),
        CONSTRAINT "FK_TRANSACTIONS_ACCOUNTS_AccountId_UserId" FOREIGN KEY ("AccountId", "UserId") REFERENCES "ACCOUNTS" ("Id", "UserId") ON DELETE RESTRICT,
        CONSTRAINT "FK_TRANSACTIONS_ACCOUNTS_DestinationAccountId_UserId" FOREIGN KEY ("DestinationAccountId", "UserId") REFERENCES "ACCOUNTS" ("Id", "UserId") ON DELETE RESTRICT,
        CONSTRAINT "FK_TRANSACTIONS_CATEGORIES_CategoryId_UserId" FOREIGN KEY ("CategoryId", "UserId") REFERENCES "CATEGORIES" ("Id", "UserId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122224023_FinancialTransactions') THEN
    CREATE TABLE "TRANSACTION_REVISIONS" (
        "Id" uuid NOT NULL,
        "UserId" text NOT NULL,
        "TransactionId" uuid NOT NULL,
        "Number" integer NOT NULL,
        "Operation" character varying(10) NOT NULL,
        "Reason" character varying(300) NOT NULL,
        "Snapshot" jsonb NOT NULL,
        "At" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_TRANSACTION_REVISIONS" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TRANSACTION_REVISIONS_TRANSACTIONS_TransactionId_UserId" FOREIGN KEY ("TransactionId", "UserId") REFERENCES "TRANSACTIONS" ("Id", "UserId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122224023_FinancialTransactions') THEN
    CREATE INDEX "IX_TRANSACTION_REVISIONS_TransactionId_UserId" ON "TRANSACTION_REVISIONS" ("TransactionId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122224023_FinancialTransactions') THEN
    CREATE INDEX "IX_TRANSACTIONS_AccountId_UserId" ON "TRANSACTIONS" ("AccountId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122224023_FinancialTransactions') THEN
    CREATE INDEX "IX_TRANSACTIONS_CategoryId_UserId" ON "TRANSACTIONS" ("CategoryId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122224023_FinancialTransactions') THEN
    CREATE INDEX "IX_TRANSACTIONS_DestinationAccountId_UserId" ON "TRANSACTIONS" ("DestinationAccountId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122224023_FinancialTransactions') THEN
    CREATE INDEX "IX_TRANSACTIONS_UserId_Date_Id" ON "TRANSACTIONS" ("UserId", "Date", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122224023_FinancialTransactions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261122224023_FinancialTransactions', '10.0.15');
    END IF;
END $EF$;
COMMIT;

