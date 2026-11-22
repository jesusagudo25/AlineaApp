START TRANSACTION;
CREATE TABLE "BANKS" (
    "Id" uuid NOT NULL,
    "UserId" text NOT NULL,
    "Name" character varying(100) NOT NULL,
    "NormalizedName" character varying(300) NOT NULL,
    "IsActive" boolean NOT NULL,
    "Version" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_BANKS" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_BANKS_Id_UserId" UNIQUE ("Id", "UserId"),
    CONSTRAINT "FK_BANKS_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "CATEGORIES" (
    "Id" uuid NOT NULL,
    "Kind" character varying(10) NOT NULL,
    "InitialKey" character varying(30),
    "UserId" text NOT NULL,
    "Name" character varying(100) NOT NULL,
    "NormalizedName" character varying(300) NOT NULL,
    "IsActive" boolean NOT NULL,
    "Version" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CATEGORIES" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_CATEGORIES_Kind" CHECK ("Kind" IN ('income','expense')),
    CONSTRAINT "FK_CATEGORIES_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "ACCOUNTS" (
    "Id" uuid NOT NULL,
    "Kind" character varying(10) NOT NULL,
    "BankId" uuid,
    "Currency" character varying(3) NOT NULL,
    "OpeningBalance" numeric(18,2) NOT NULL,
    "OpeningDate" date NOT NULL,
    "OpeningLockedAt" timestamp with time zone,
    "UserId" text NOT NULL,
    "Name" character varying(100) NOT NULL,
    "NormalizedName" character varying(300) NOT NULL,
    "IsActive" boolean NOT NULL,
    "Version" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ACCOUNTS" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_ACCOUNTS_CashBalance" CHECK ("Kind" <> 'cash' OR "OpeningBalance" >= 0),
    CONSTRAINT "CK_ACCOUNTS_Currency" CHECK ("Currency" = 'USD'),
    CONSTRAINT "CK_ACCOUNTS_KindBank" CHECK (("Kind" = 'bank' AND "BankId" IS NOT NULL) OR ("Kind" = 'cash' AND "BankId" IS NULL)),
    CONSTRAINT "FK_ACCOUNTS_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ACCOUNTS_BANKS_BankId_UserId" FOREIGN KEY ("BankId", "UserId") REFERENCES "BANKS" ("Id", "UserId") ON DELETE RESTRICT
);

CREATE INDEX "IX_ACCOUNTS_BankId_UserId" ON "ACCOUNTS" ("BankId", "UserId");

CREATE UNIQUE INDEX "IX_ACCOUNTS_UserId_NormalizedName" ON "ACCOUNTS" ("UserId", "NormalizedName");

CREATE UNIQUE INDEX "IX_BANKS_UserId_NormalizedName" ON "BANKS" ("UserId", "NormalizedName");

CREATE UNIQUE INDEX "IX_CATEGORIES_UserId_InitialKey" ON "CATEGORIES" ("UserId", "InitialKey");

CREATE UNIQUE INDEX "IX_CATEGORIES_UserId_Kind_NormalizedName" ON "CATEGORIES" ("UserId", "Kind", "NormalizedName");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261122221226_FinancialCatalogs', '10.0.15');

COMMIT;

