CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE "CONFIG_GENERAL" (
    "Clave" character varying(100) NOT NULL,
    "Valor" character varying(500) NOT NULL,
    CONSTRAINT "PK_CONFIG_GENERAL" PRIMARY KEY ("Clave")
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261111023309_InitialConfiguration', '10.0.15');

COMMIT;

