using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Alinea.Api.Features.Finance;
using Alinea.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using static Alinea.Api.Tests.FinanceTests;

namespace Alinea.Api.Tests;

public sealed class TransactionTests
{
    private static object Input(JsonNode account, JsonNode? category, string kind = "expense", string amount = "10.01", JsonNode? destination = null, Guid? key = null, string? version = null, string? reason = null, string date = "2026-11-10") =>
        new { requestId = key ?? Guid.NewGuid(), accountId = account["id"]!.GetValue<Guid>(), categoryId = category?["id"]!.GetValue<Guid>(), kind, amount,
            destinationAccountId = destination?["id"]!.GetValue<Guid>(), date, description = "Ensayo de libro", version, reason };
    private static async Task<(JsonNode Account, JsonNode Other, JsonNode Income, JsonNode Expense)> Setup(HttpClient client) => (
        await Create(client, "accounts", AccountBody("Caja uno", amount: "100.00", kind: "cash")),
        await Create(client, "accounts", AccountBody("Caja dos", amount: "20.00", kind: "cash")),
        await Create(client, "categories", new { name = "Entradas", kind = "income" }),
        await Create(client, "categories", new { name = "Salidas", kind = "expense" }));
    private static async Task<string> Balance(HttpClient client, JsonNode account) => (await Json(await client.GetAsync("/api/finance/accounts/" + account["id"]!.GetValue<Guid>())))["currentBalance"]!.GetValue<string>();

    [Fact]
    public async Task Ledger_balances_reports_filters_pagination_and_detail_use_real_postgres()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101");
        var s = await Setup(client);
        await Create(client, "transactions", Input(s.Account, s.Income, "income", "50.10"));
        await Create(client, "transactions", Input(s.Account, s.Expense, amount: "10.01"));
        var transfer = await Create(client, "transactions", Input(s.Account, null, "transfer", "30.00", s.Other));
        Assert.Equal("110.09", await Balance(client, s.Account)); Assert.Equal("50.00", await Balance(client, s.Other));
        var list = await Json(await client.GetAsync("/api/finance/transactions?pageSize=2"));
        Assert.Equal(3, list["total"]!.GetValue<int>()); Assert.Equal(2, list["items"]!.AsArray().Count);
        Assert.Equal("50.10", list["income"]!.GetValue<string>()); Assert.Equal("10.01", list["expense"]!.GetValue<string>());
        Assert.Single((await Json(await client.GetAsync("/api/finance/transactions?page=2&pageSize=2")))["items"]!.AsArray());
        var filtered = await Json(await client.GetAsync("/api/finance/transactions?kind=transfer&accountId=" + s.Other["id"]!.GetValue<Guid>() + "&from=2026-11-10&to=2026-11-10"));
        Assert.Equal(1, filtered["total"]!.GetValue<int>()); Assert.Equal("0.00", filtered["income"]!.GetValue<string>()); Assert.Equal("0.00", filtered["expense"]!.GetValue<string>());
        Assert.Equal(1, (await Json(await client.GetAsync("/api/finance/transactions?categoryId=" + s.Expense["id"]!.GetValue<Guid>())))["total"]!.GetValue<int>());
        Assert.Single((await Json(await client.GetAsync("/api/finance/transactions/" + transfer["id"]!.GetValue<Guid>())))["revisions"]!.AsArray());
        foreach (var query in new[] { "page=0", "pageSize=101", "kind=card", "from=2026-11-11&to=2026-11-10", "from=invalid" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/finance/transactions?" + query)).StatusCode);
    }

    [Fact]
    public async Task Editing_and_voiding_preserve_audit_opening_and_idempotent_responses()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101"); var s = await Setup(client);
        var row = await Create(client, "transactions", Input(s.Account, null, "transfer", "30.00", s.Other)); var path = "/api/finance/transactions/" + row["id"]!.GetValue<Guid>();
        var edit = Input(s.Account, null, "transfer", "40.00", s.Other, version: row["version"]!.GetValue<string>(), reason: "Corrección de importe");
        var edited = await Json(await Write(client, path, edit, "PUT"));
        Assert.Equal("60.00", await Balance(client, s.Account)); Assert.Equal("60.00", await Balance(client, s.Other));
        Assert.Equal(HttpStatusCode.Conflict, (await Write(client, path, Input(s.Account, s.Expense, version: row["version"]!.GetValue<string>(), reason: "Obsoleto"), "PUT")).StatusCode);
        var voidBody = new { requestId = Guid.NewGuid(), version = edited["version"]!.GetValue<Guid>(), reason = "Movimiento duplicado" };
        var voided = await Write(client, path + "/void", voidBody); Assert.Equal(HttpStatusCode.OK, voided.StatusCode);
        Assert.True(JsonNode.DeepEquals(await Json(voided), await Json(await Write(client, path + "/void", voidBody))));
        Assert.Equal("100.00", await Balance(client, s.Account)); Assert.Equal("20.00", await Balance(client, s.Other));
        Assert.Equal(3, (await Json(await client.GetAsync(path)))["revisions"]!.AsArray().Count);
        var account = await Json(await client.GetAsync("/api/finance/accounts/" + s.Account["id"]!.GetValue<Guid>())); Assert.True(account["openingLocked"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Conflict, (await Write(client, "/api/finance/accounts/" + account["id"]!.GetValue<Guid>(), AccountBody("Caja uno", amount: "101.00", kind: "cash", version: account["version"]!.GetValue<string>(), confirm: true), "PUT")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.DeleteAsync(path)).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>();
        Assert.Single(await db.Transactions.ToListAsync()); Assert.Equal(3, await db.TransactionRevisions.CountAsync());
        await Assert.ThrowsAsync<PostgresException>(() => db.Transactions.ExecuteDeleteAsync());
    }

    [Fact]
    public async Task Concurrent_retries_and_stale_edits_never_duplicate_or_lose_effects()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101"); var s = await Setup(client);
        var key = Guid.NewGuid(); var body = Input(s.Account, s.Expense, key: key);
        var replies = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Write(client, "/api/finance/transactions", body)));
        Assert.All(replies, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var row = await Json(replies[0]); Assert.Equal("89.99", await Balance(client, s.Account));
        Assert.Equal(HttpStatusCode.Conflict, (await Write(client, "/api/finance/transactions", Input(s.Account, s.Expense, amount: "12.00", key: key))).StatusCode);
        var path = "/api/finance/transactions/" + row["id"]!.GetValue<Guid>();
        var edits = await Task.WhenAll(Write(client, path, Input(s.Account, s.Expense, amount: "20.00", version: row["version"]!.GetValue<string>(), reason: "Primera corrección"), "PUT"),
            Write(client, path, Input(s.Account, s.Expense, amount: "20.00", version: row["version"]!.GetValue<string>(), reason: "Segunda corrección"), "PUT"));
        Assert.Single(edits, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(edits, x => x.StatusCode == HttpStatusCode.Conflict);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Write(client, "/api/finance/transactions", Input(s.Account, null, "transfer", "1.01", s.Other))));
        Assert.Equal("71.92", await Balance(client, s.Account)); Assert.Equal("28.08", await Balance(client, s.Other));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>(); Assert.Equal(9, await db.Transactions.CountAsync());
    }

    [Fact]
    public async Task Ownership_csrf_and_database_composite_constraints_are_enforced()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101"); using var other = await User(factory, "+999000000102"); var s = await Setup(client); var foreign = await Setup(other);
        using var anonymous = factory.Browser(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/finance/transactions")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/finance/transactions", Input(s.Account, s.Expense))).StatusCode);
        var row = await Create(client, "transactions", Input(s.Account, s.Expense)); var path = "/api/finance/transactions/" + row["id"]!.GetValue<Guid>();
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode);
        Assert.Equal(0, (await Json(await other.GetAsync("/api/finance/transactions")))["total"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.NotFound, (await Write(other, path, Input(foreign.Account, foreign.Expense, version: row["version"]!.GetValue<string>(), reason: "Ajeno"), "PUT")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Write(other, path + "/void", new { requestId = Guid.NewGuid(), version = row["version"]!.GetValue<Guid>(), reason = "Ajeno" })).StatusCode);
        foreach (var body in new[] { Input(foreign.Account, s.Expense), Input(s.Account, foreign.Expense), Input(s.Account, null, "transfer", destination: foreign.Account) })
            Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, "/api/finance/transactions", body)).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>();
        var owner = (await db.Accounts.SingleAsync(x => x.Id == s.Account["id"]!.GetValue<Guid>())).UserId;
        db.Transactions.Add(new FinancialTransaction { UserId = owner, Kind = "expense", AccountId = foreign.Account["id"]!.GetValue<Guid>(), CategoryId = s.Expense["id"]!.GetValue<Guid>(), Amount = 1, Date = new DateOnly(2026,11,10) });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal("23503", Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Theory]
    [InlineData("0.00", "2026-11-10")]
    [InlineData("-1.00", "2026-11-10")]
    [InlineData("1.001", "2026-11-10")]
    [InlineData("10000000000000000.00", "2026-11-10")]
    [InlineData("1.00", "2026-11-09")]
    [InlineData("1.00", "2026-11-11")]
    [InlineData("1.00", "2026-02-30")]
    public async Task Invalid_amounts_and_dates_leave_no_effects(string amount, string date)
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101"); var s = await Setup(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, "/api/finance/transactions", Input(s.Account, s.Expense, amount: amount, date: date))).StatusCode);
        Assert.Equal("100.00", await Balance(client, s.Account));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>(); Assert.Empty(await db.Transactions.ToListAsync()); Assert.Empty(await db.FinancialRequests.ToListAsync());
    }

    [Fact]
    public async Task Audit_failure_rolls_back_both_transfer_effects_opening_locks_and_request_key()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101"); var s = await Setup(client);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_reject_revision() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Injected test dependency failure'; END $$;
            CREATE TRIGGER test_revision_failure BEFORE INSERT ON "TRANSACTION_REVISIONS" FOR EACH ROW EXECUTE FUNCTION test_reject_revision();
            """);
        var body = Input(s.Account, null, "transfer", "30.00", s.Other);
        try
        {
            var failed = await Write(client, "/api/finance/transactions", body); Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            Assert.DoesNotContain("Injected", await failed.Content.ReadAsStringAsync());
            Assert.Equal("100.00", await Balance(client, s.Account)); Assert.Equal("20.00", await Balance(client, s.Other));
            Assert.Empty(await db.Transactions.AsNoTracking().ToListAsync()); Assert.Empty(await db.FinancialRequests.AsNoTracking().ToListAsync());
            Assert.All(await db.Accounts.AsNoTracking().ToListAsync(), a => Assert.Null(a.OpeningLockedAt));
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_revision_failure ON \"TRANSACTION_REVISIONS\"; DROP FUNCTION test_reject_revision();"); }
        Assert.Equal(HttpStatusCode.Created, (await Write(client, "/api/finance/transactions", body)).StatusCode);
        Assert.Equal("70.00", await Balance(client, s.Account)); Assert.Equal("50.00", await Balance(client, s.Other));
    }

    [Fact]
    public async Task Category_types_and_inactive_references_are_controlled_and_void_remains_available()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101"); var s = await Setup(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, "/api/finance/transactions", Input(s.Account, s.Income))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, "/api/finance/transactions", Input(s.Account, null, "transfer", destination: s.Account))).StatusCode);
        var row = await Create(client, "transactions", Input(s.Account, s.Expense));
        Assert.Equal(HttpStatusCode.Conflict, (await Write(client, "/api/finance/categories/" + s.Expense["id"]!.GetValue<Guid>(), new { name = "Salidas", kind = "income", version = s.Expense["version"]!.GetValue<Guid>() }, "PUT")).StatusCode);
        await Write(client, "/api/finance/categories/" + s.Expense["id"]!.GetValue<Guid>(), new { name = "Salidas", kind = "expense", isActive = false, version = s.Expense["version"]!.GetValue<Guid>() }, "PUT");
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, "/api/finance/transactions", Input(s.Account, s.Expense))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Write(client, "/api/finance/transactions/" + row["id"]!.GetValue<Guid>() + "/void", new { requestId = Guid.NewGuid(), version = row["version"]!.GetValue<Guid>(), reason = "Anulación válida" })).StatusCode);
    }
}
