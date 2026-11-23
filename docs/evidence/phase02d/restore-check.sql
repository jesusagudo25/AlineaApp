SELECT count(*) AS migrations FROM "__EFMigrationsHistory";
SELECT count(*) AS cards, md5(string_agg(row_to_json(c)::text, '' ORDER BY "Id")) AS checksum FROM "PAYMENT_CARDS" c;
SELECT count(*) AS commitments, md5(string_agg(row_to_json(c)::text, '' ORDER BY "Id")) AS checksum FROM "COMMITMENTS" c;
SELECT count(*) AS occurrences, md5(string_agg(row_to_json(o)::text, '' ORDER BY "Id")) AS checksum FROM "COMMITMENT_OCCURRENCES" o;
SELECT count(*) AS transactions, count(*) FILTER (WHERE "IsVoided") AS voided,
       sum(CASE WHEN NOT "IsVoided" AND "Kind"='expense' THEN "Amount" ELSE 0 END) AS expense,
       md5(string_agg(row_to_json(t)::text, '' ORDER BY "Id")) AS checksum FROM "TRANSACTIONS" t;
SELECT count(*) AS revisions, md5(string_agg(row_to_json(r)::text, '' ORDER BY "Id")) AS checksum FROM "TRANSACTION_REVISIONS" r;
SELECT count(*) AS requests, md5(string_agg(row_to_json(r)::text, '' ORDER BY "UserId", "RequestId")) AS checksum FROM "FINANCIAL_REQUESTS" r;
SELECT sum(a."OpeningBalance" + COALESCE((SELECT sum(CASE WHEN t."Kind"='income' THEN t."Amount" ELSE -t."Amount" END) FROM "TRANSACTIONS" t WHERE t."AccountId"=a."Id" AND t."UserId"=a."UserId" AND NOT t."IsVoided"),0)
       + COALESCE((SELECT sum(t."Amount") FROM "TRANSACTIONS" t WHERE t."DestinationAccountId"=a."Id" AND t."UserId"=a."UserId" AND NOT t."IsVoided"),0)) AS cash FROM "ACCOUNTS" a;
SELECT sum(c."InitialDebt" + COALESCE((SELECT sum(CASE WHEN t."Kind"='expense' THEN t."Amount" ELSE -t."Amount" END) FROM "TRANSACTIONS" t WHERE t."CardId"=c."Id" AND t."UserId"=c."UserId" AND NOT t."IsVoided"),0)) AS debt FROM "PAYMENT_CARDS" c WHERE c."Kind"='credit';
