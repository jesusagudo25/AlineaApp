-- Synthetic migration fixture, executed only in a NEW alinea02d_upgrade_* database.
INSERT INTO "AspNetUsers" ("Id", "IsDisabled", "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
VALUES ('migration-fixture', false, false, false, false, false, 0);
INSERT INTO "ACCOUNTS" ("Id", "UserId", "Name", "NormalizedName", "IsActive", "Version", "CreatedAt", "UpdatedAt", "Kind", "Currency", "OpeningBalance", "OpeningDate", "OpeningLockedAt")
VALUES ('f0000000-0000-0000-0000-000000000001', 'migration-fixture', 'Migration fixture', 'MIGRATION FIXTURE', true, 'f0000000-0000-0000-0000-000000000005', '2026-11-10T12:00:00Z', '2026-11-10T12:00:00Z', 'cash', 'USD', 100.00, '2026-11-10', '2026-11-10T12:00:00Z');
INSERT INTO "CATEGORIES" ("Id", "UserId", "Name", "NormalizedName", "IsActive", "Version", "CreatedAt", "UpdatedAt", "Kind", "UsedByTransactions")
VALUES ('f0000000-0000-0000-0000-000000000002', 'migration-fixture', 'Migration fixture', 'MIGRATION FIXTURE', true, 'f0000000-0000-0000-0000-000000000005', '2026-11-10T12:00:00Z', '2026-11-10T12:00:00Z', 'expense', true);
INSERT INTO "TRANSACTIONS" ("Id", "UserId", "Kind", "AccountId", "CategoryId", "Amount", "Date", "Description", "IsVoided", "Version", "CreatedAt", "UpdatedAt")
VALUES ('f0000000-0000-0000-0000-000000000003', 'migration-fixture', 'expense', 'f0000000-0000-0000-0000-000000000001', 'f0000000-0000-0000-0000-000000000002', 12.34, '2026-11-10', 'Migration fixture', false, 'f0000000-0000-0000-0000-000000000005', '2026-11-10T12:00:00Z', '2026-11-10T12:00:00Z');
INSERT INTO "TRANSACTION_REVISIONS" ("Id", "UserId", "TransactionId", "Number", "Operation", "Reason", "At", "Snapshot")
VALUES ('f0000000-0000-0000-0000-000000000004', 'migration-fixture', 'f0000000-0000-0000-0000-000000000003', 1, 'create', 'Migration fixture', '2026-11-10T12:00:00Z',
'{"id":"f0000000-0000-0000-0000-000000000003","version":"f0000000-0000-0000-0000-000000000005","kind":"expense","accountId":"f0000000-0000-0000-0000-000000000001","categoryId":"f0000000-0000-0000-0000-000000000002","destinationAccountId":null,"amount":"12.34","date":"2026-11-10","description":"Migration fixture","isVoided":false}');
