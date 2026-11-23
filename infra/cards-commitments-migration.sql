START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    ALTER TABLE "TRANSACTIONS" DROP CONSTRAINT "CK_TRANSACTIONS_Shape";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    ALTER TABLE "TRANSACTIONS" ALTER COLUMN "Kind" TYPE character varying(15);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    ALTER TABLE "TRANSACTIONS" ALTER COLUMN "AccountId" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    ALTER TABLE "TRANSACTIONS" ADD "CardId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    ALTER TABLE "TRANSACTIONS" ADD "CommitmentOccurrenceId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    CREATE TABLE "COMMITMENTS" (
        "Id" uuid NOT NULL,
        "Kind" character varying(10) NOT NULL,
        "CategoryId" uuid NOT NULL,
        "Amount" numeric(18,2) NOT NULL,
        "Frequency" character varying(15) NOT NULL,
        "NextDate" date,
        "AnchorDay" integer NOT NULL,
        "Status" character varying(10) NOT NULL,
        "UserId" text NOT NULL,
        "Name" character varying(100) NOT NULL,
        "NormalizedName" character varying(300) NOT NULL,
        "IsActive" boolean NOT NULL,
        "Version" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_COMMITMENTS" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_COMMITMENTS_Id_UserId" UNIQUE ("Id", "UserId"),
        CONSTRAINT "CK_COMMITMENTS_Values" CHECK ("Kind" IN ('income','expense') AND "Amount">0 AND "Frequency" IN ('once','weekly','fortnightly','monthly') AND "Status" IN ('active','paused','inactive') AND "AnchorDay" BETWEEN 1 AND 31),
        CONSTRAINT "FK_COMMITMENTS_CATEGORIES_CategoryId_UserId" FOREIGN KEY ("CategoryId", "UserId") REFERENCES "CATEGORIES" ("Id", "UserId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    CREATE TABLE "PAYMENT_CARDS" (
        "Id" uuid NOT NULL,
        "Kind" character varying(10) NOT NULL,
        "AccountId" uuid,
        "CreditLimit" numeric(18,2) NOT NULL,
        "InitialDebt" numeric(18,2) NOT NULL,
        "OpeningDate" date NOT NULL,
        "ClosingDay" integer,
        "PaymentDay" integer,
        "Used" boolean NOT NULL,
        "UserId" text NOT NULL,
        "Name" character varying(100) NOT NULL,
        "NormalizedName" character varying(300) NOT NULL,
        "IsActive" boolean NOT NULL,
        "Version" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_PAYMENT_CARDS" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_PAYMENT_CARDS_Id_UserId" UNIQUE ("Id", "UserId"),
        CONSTRAINT "CK_CARDS_Shape" CHECK (("Kind"='debit' AND "AccountId" IS NOT NULL AND "CreditLimit"=0 AND "InitialDebt"=0 AND "ClosingDay" IS NULL AND "PaymentDay" IS NULL) OR ("Kind"='credit' AND "AccountId" IS NULL AND "CreditLimit">0 AND "InitialDebt">=0 AND "ClosingDay" BETWEEN 1 AND 31 AND "PaymentDay" BETWEEN 1 AND 31 AND "ClosingDay" IS NOT NULL AND "PaymentDay" IS NOT NULL)),
        CONSTRAINT "FK_PAYMENT_CARDS_ACCOUNTS_AccountId_UserId" FOREIGN KEY ("AccountId", "UserId") REFERENCES "ACCOUNTS" ("Id", "UserId") ON DELETE RESTRICT,
        CONSTRAINT "FK_PAYMENT_CARDS_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    CREATE TABLE "COMMITMENT_OCCURRENCES" (
        "Id" uuid NOT NULL,
        "UserId" text NOT NULL,
        "CommitmentId" uuid NOT NULL,
        "DueDate" date NOT NULL,
        "Concept" character varying(100) NOT NULL,
        "Kind" character varying(10) NOT NULL,
        "CategoryId" uuid NOT NULL,
        "Amount" numeric(18,2) NOT NULL,
        CONSTRAINT "PK_COMMITMENT_OCCURRENCES" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_COMMITMENT_OCCURRENCES_Id_UserId" UNIQUE ("Id", "UserId"),
        CONSTRAINT "FK_COMMITMENT_OCCURRENCES_CATEGORIES_CategoryId_UserId" FOREIGN KEY ("CategoryId", "UserId") REFERENCES "CATEGORIES" ("Id", "UserId") ON DELETE RESTRICT,
        CONSTRAINT "FK_COMMITMENT_OCCURRENCES_COMMITMENTS_CommitmentId_UserId" FOREIGN KEY ("CommitmentId", "UserId") REFERENCES "COMMITMENTS" ("Id", "UserId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    CREATE INDEX "IX_TRANSACTIONS_CardId_UserId" ON "TRANSACTIONS" ("CardId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    CREATE UNIQUE INDEX "IX_TRANSACTIONS_CommitmentOccurrenceId" ON "TRANSACTIONS" ("CommitmentOccurrenceId") WHERE "CommitmentOccurrenceId" IS NOT NULL AND NOT "IsVoided";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    CREATE INDEX "IX_TRANSACTIONS_CommitmentOccurrenceId_UserId" ON "TRANSACTIONS" ("CommitmentOccurrenceId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    ALTER TABLE "TRANSACTIONS" ADD CONSTRAINT "CK_TRANSACTIONS_Shape" CHECK (("Kind" IN ('income','expense') AND "CategoryId" IS NOT NULL AND "DestinationAccountId" IS NULL AND ("AccountId" IS NOT NULL OR ("Kind"='expense' AND "CardId" IS NOT NULL))) OR ("Kind"='transfer' AND "AccountId" IS NOT NULL AND "CardId" IS NULL AND "CategoryId" IS NULL AND "DestinationAccountId" IS NOT NULL AND "AccountId"<>"DestinationAccountId") OR ("Kind"='card_payment' AND "CardId" IS NOT NULL AND "AccountId" IS NOT NULL AND "CategoryId" IS NULL AND "DestinationAccountId" IS NULL));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    CREATE INDEX "IX_COMMITMENT_OCCURRENCES_CategoryId_UserId" ON "COMMITMENT_OCCURRENCES" ("CategoryId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    CREATE UNIQUE INDEX "IX_COMMITMENT_OCCURRENCES_CommitmentId_DueDate" ON "COMMITMENT_OCCURRENCES" ("CommitmentId", "DueDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    CREATE INDEX "IX_COMMITMENT_OCCURRENCES_CommitmentId_UserId" ON "COMMITMENT_OCCURRENCES" ("CommitmentId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    CREATE INDEX "IX_COMMITMENTS_CategoryId_UserId" ON "COMMITMENTS" ("CategoryId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    CREATE INDEX "IX_COMMITMENTS_UserId_NextDate" ON "COMMITMENTS" ("UserId", "NextDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    CREATE INDEX "IX_PAYMENT_CARDS_AccountId_UserId" ON "PAYMENT_CARDS" ("AccountId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    CREATE UNIQUE INDEX "IX_PAYMENT_CARDS_UserId_NormalizedName" ON "PAYMENT_CARDS" ("UserId", "NormalizedName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    ALTER TABLE "TRANSACTIONS" ADD CONSTRAINT "FK_TRANSACTIONS_COMMITMENT_OCCURRENCES_CommitmentOccurrenceId_~" FOREIGN KEY ("CommitmentOccurrenceId", "UserId") REFERENCES "COMMITMENT_OCCURRENCES" ("Id", "UserId") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    ALTER TABLE "TRANSACTIONS" ADD CONSTRAINT "FK_TRANSACTIONS_PAYMENT_CARDS_CardId_UserId" FOREIGN KEY ("CardId", "UserId") REFERENCES "PAYMENT_CARDS" ("Id", "UserId") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261123001813_CardsAndCommitments') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261123001813_CardsAndCommitments', '10.0.15');
    END IF;
END $EF$;
COMMIT;

