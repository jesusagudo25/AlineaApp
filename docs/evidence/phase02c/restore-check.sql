SELECT count(*) AS migrations FROM "__EFMigrationsHistory";
SELECT count(*) AS transactions, sum("Amount") AS amount,
       sum(CASE WHEN "Kind" = 'income' AND NOT "IsVoided" THEN "Amount" ELSE 0 END) AS income,
       sum(CASE WHEN "Kind" = 'expense' AND NOT "IsVoided" THEN "Amount" ELSE 0 END) AS expense,
       md5(string_agg(row_to_json(t)::text, '' ORDER BY "Id")) AS checksum
FROM "TRANSACTIONS" t;
SELECT count(*) AS revisions, md5(string_agg(row_to_json(r)::text, '' ORDER BY "Id")) AS checksum FROM "TRANSACTION_REVISIONS" r;
SELECT count(*) AS requests, md5(string_agg(row_to_json(r)::text, '' ORDER BY "UserId", "RequestId")) AS checksum FROM "FINANCIAL_REQUESTS" r;
SELECT count(*) AS accounts, sum(a."OpeningBalance") AS opening,
       sum(a."OpeningBalance" + COALESCE((SELECT sum(CASE WHEN t."Kind" = 'income' THEN t."Amount" ELSE -t."Amount" END) FROM "TRANSACTIONS" t WHERE t."UserId" = a."UserId" AND t."AccountId" = a."Id" AND NOT t."IsVoided"), 0)
       + COALESCE((SELECT sum(t."Amount") FROM "TRANSACTIONS" t WHERE t."UserId" = a."UserId" AND t."DestinationAccountId" = a."Id" AND NOT t."IsVoided"), 0)) AS current
FROM "ACCOUNTS" a;
