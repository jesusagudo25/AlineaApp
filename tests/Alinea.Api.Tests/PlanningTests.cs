using System.Net;
using System.Text.Json.Nodes;
using Alinea.Api.Features.Finance;
using Alinea.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using static Alinea.Api.Tests.FinanceTests;

namespace Alinea.Api.Tests;
public sealed class PlanningTests
{
    private static Guid Id(JsonNode row) => row["id"]!.GetValue<Guid>();
    private static Guid Version(JsonNode row) => row["version"]!.GetValue<Guid>();
    private static object Card(JsonNode? account = null, Guid? key = null, Guid? version = null, bool active = true, string initialDebt = "100.00", bool confirm = false) => new {
        requestId = key ?? Guid.NewGuid(), name = account is null ? "Crédito ensayo" : "Débito ensayo", kind = account is null ? "credit" : "debit", accountId = account is null ? (Guid?)null : Id(account), openingDate = "2026-11-10",
        creditLimit = account is null ? "1000.00" : "0.00", initialDebt = account is null ? initialDebt : "0.00", closingDay = account is null ? (int?)20 : null, paymentDay = account is null ? (int?)5 : null, isActive = active, version, confirmOpeningChange = confirm };
    private static object Operation(JsonNode card, JsonNode? account, JsonNode? category, string kind = "expense", string amount = "50.00", Guid? key = null) => new {
        requestId = key ?? Guid.NewGuid(), cardId = Id(card), accountId = account is null ? (Guid?)null : Id(account), categoryId = category is null ? (Guid?)null : Id(category), kind, amount, date = "2026-11-10", description = "Operación de tarjeta" };
    private static object Plan(JsonNode category, string amount = "20.00", string frequency = "monthly", string status = "active", Guid? version = null, string date = "2026-11-10") => new {
        requestId = Guid.NewGuid(), name = "Compromiso ensayo", categoryId = Id(category), kind = category["kind"]!.GetValue<string>(), amount, frequency, nextDate = date, status, version };
    private static object Confirmation(JsonNode plan, JsonNode account, Guid? key = null) => new { requestId = key ?? Guid.NewGuid(), version = Version(plan), dueDate = "2026-11-10", date = "2026-11-10", accountId = Id(account) };
    private static async Task<JsonNode> Get(HttpClient client, string path) => await Json(await client.GetAsync("/api/finance/" + path));
    private static async Task<(JsonNode Account, JsonNode Category)> Setup(HttpClient client) => (await Create(client, "accounts", AccountBody("Caja", kind: "cash", amount: "100.00")), await Create(client, "categories", new { name = "Gasto", kind = "expense" }));
    private static async Task Void(HttpClient client, JsonNode transaction) => Assert.Equal(HttpStatusCode.OK, (await Write(client, "/api/finance/transactions/" + Id(transaction) + "/void", new { requestId = Guid.NewGuid(), version = Version(transaction), reason = "Corrección verificada" })).StatusCode);

    [Fact]
    public async Task Credit_purchases_payments_and_voids_change_debt_without_double_expense()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101"); var s = await Setup(client);
        var card = await Create(client, "cards", Card());
        var purchase = await Create(client, "transactions", Operation(card, null, s.Category));
        Assert.Equal("100.00", (await Get(client, "accounts/" + Id(s.Account)))["currentBalance"]!.GetValue<string>());
        var payment = await Create(client, "transactions", Operation(card, s.Account, null, "card_payment", "40.00"));
        var actual = await Get(client, "cards/" + Id(card)); Assert.Equal("110.00", actual["currentDebt"]!.GetValue<string>()); Assert.Equal("890.00", actual["availableCredit"]!.GetValue<string>());
        Assert.Equal("60.00", (await Get(client, "accounts/" + Id(s.Account)))["currentBalance"]!.GetValue<string>());
        Assert.Equal("50.00", (await Get(client, "transactions"))["expense"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Conflict, (await Write(client, "/api/finance/cards/" + Id(card), Card(version: Version(actual), initialDebt: "90.00", confirm: true), "PUT")).StatusCode);
        await Void(client, purchase); Assert.Equal("60.00", (await Get(client, "cards/" + Id(card)))["currentDebt"]!.GetValue<string>()); Assert.Equal("0.00", (await Get(client, "transactions"))["expense"]!.GetValue<string>());
        await Void(client, payment); Assert.Equal("100.00", (await Get(client, "cards/" + Id(card)))["currentDebt"]!.GetValue<string>());
        Assert.Equal(2, (await Get(client, "transactions/" + Id(payment)))["revisions"]!.AsArray().Count);
    }

    [Fact]
    public async Task Debit_card_has_one_cash_expense_and_rejects_wrong_account_or_payment()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101"); var s = await Setup(client);
        var card = await Create(client, "cards", Card(s.Account));
        await Create(client, "transactions", Operation(card, s.Account, s.Category));
        Assert.Equal("50.00", (await Get(client, "accounts/" + Id(s.Account)))["currentBalance"]!.GetValue<string>());
        Assert.Equal("50.00", (await Get(client, "transactions"))["expense"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, "/api/finance/transactions", Operation(card, null, s.Category))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, "/api/finance/transactions", Operation(card, s.Account, null, "card_payment"))).StatusCode);
        var latest = await Get(client, "cards/" + Id(card)); await Write(client, "/api/finance/cards/" + Id(card), Card(s.Account, version: Version(latest), active: false), "PUT");
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, "/api/finance/transactions", Operation(card, s.Account, s.Category))).StatusCode);
    }

    [Fact]
    public async Task Planning_does_not_spend_future_income_and_void_restores_pending_snapshot()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101"); var s = await Setup(client);
        var plan = await Create(client, "commitments", Plan(s.Category));
        var income = await Create(client, "categories", new { name = "Previsto", kind = "income" }); await Create(client, "commitments", Plan(income, "1000.00", "once"));
        var query = "commitments/pending?from=2026-11-10&to=2026-12-10";
        var pending = await Get(client, query); Assert.Equal("100.00", pending["actualAvailable"]!.GetValue<string>()); Assert.Equal("40.00", pending["pendingExpenses"]!.GetValue<string>()); Assert.Equal("60.00", pending["estimatedAvailable"]!.GetValue<string>());
        var confirmed = await Create(client, "commitments/" + Id(plan) + "/confirm", Confirmation(plan, s.Account));
        Assert.NotNull(confirmed["commitmentOccurrenceId"]); Assert.Equal("80.00", (await Get(client, query))["actualAvailable"]!.GetValue<string>());
        var latest = await Get(client, "commitments/" + Id(plan)); Assert.Equal("2026-12-10", latest["nextDate"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, (await Write(client, "/api/finance/commitments/" + Id(plan), Plan(s.Category, "30.00", version: Version(latest), date: "2026-12-10"), "PUT")).StatusCode);
        await Void(client, confirmed); pending = await Get(client, query); Assert.Equal("50.00", pending["pendingExpenses"]!.GetValue<string>()); Assert.Equal("100.00", pending["actualAvailable"]!.GetValue<string>());
        latest = await Get(client, "commitments/" + Id(plan)); var again = await Create(client, "commitments/" + Id(plan) + "/confirm", Confirmation(latest, s.Account));
        Assert.Equal("20.00", again["amount"]!.GetValue<string>()); Assert.Equal(confirmed["commitmentOccurrenceId"]!.GetValue<Guid>(), again["commitmentOccurrenceId"]!.GetValue<Guid>());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>(); Assert.Equal(2, await db.Transactions.CountAsync()); Assert.Single(await db.CommitmentOccurrences.ToListAsync());
    }

    [Fact]
    public async Task Concurrent_confirmation_and_retries_create_one_occurrence_and_one_transaction()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101"); var s = await Setup(client);
        var plan = await Create(client, "commitments", Plan(s.Category, frequency: "once")); var body = Confirmation(plan, s.Account);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Write(client, "/api/finance/commitments/" + Id(plan) + "/confirm", body)));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode)); var first = await Json(results[0]);
        Assert.All(await Task.WhenAll(results.Select(Json)), row => Assert.True(JsonNode.DeepEquals(first, row)));
        var latest = await Get(client, "commitments/" + Id(plan)); Assert.Null(latest["nextDate"]);
        Assert.Equal(HttpStatusCode.Conflict, (await Write(client, "/api/finance/commitments/" + Id(plan) + "/confirm", Confirmation(latest, s.Account))).StatusCode);
        Assert.Empty((await Get(client, "commitments/pending?from=2026-11-10&to=2026-11-10"))["items"]!.AsArray());
        await Void(client, first); Assert.Single((await Get(client, "commitments/pending?from=2026-11-10&to=2026-11-10"))["items"]!.AsArray());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>(); Assert.Single(await db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task Ownership_csrf_relations_pause_and_deactivation_are_enforced()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101"); using var other = await User(factory, "+999000000102"); var s = await Setup(client);
        var card = await Create(client, "cards", Card(s.Account)); var plan = await Create(client, "commitments", Plan(s.Category));
        using var anonymous = factory.Browser(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/finance/cards")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/finance/commitments/" + Id(plan) + "/confirm", System.Net.Http.Json.JsonContent.Create(Confirmation(plan, s.Account)))).StatusCode);
        foreach (var resource in new[] { ("cards", card), ("commitments", plan) })
        { Assert.Empty((await Get(other, resource.Item1)).AsArray()); Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/finance/" + resource.Item1 + "/" + Id(resource.Item2))).StatusCode); }
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(other, "/api/finance/cards", Card(s.Account))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(other, "/api/finance/commitments", Plan(s.Category))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Write(other, "/api/finance/commitments/" + Id(plan) + "/confirm", Confirmation(plan, s.Account))).StatusCode);
        foreach (var status in new[] { "paused", "inactive" })
        {
            var current = await Get(client, "commitments/" + Id(plan)); await Write(client, "/api/finance/commitments/" + Id(plan), Plan(s.Category, status: status, version: Version(current)), "PUT");
            Assert.Empty((await Get(client, "commitments/pending?from=2026-11-10&to=2026-12-10"))["items"]!.AsArray());
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/finance/commitments/pending?from=2026-01-01&to=2028-01-01")).StatusCode);
    }

    [Fact]
    public async Task Card_idempotency_ownership_and_database_relationships_are_enforced()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101"); using var other = await User(factory, "+999000000102"); var s = await Setup(client); var foreign = await Setup(other);
        var key = Guid.NewGuid(); var body = Card(key: key);
        var replies = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Write(client, "/api/finance/cards", body))); Assert.All(replies, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var card = await Json(replies[0]); Assert.Equal(HttpStatusCode.Conflict, (await Write(client, "/api/finance/cards", Card(key: key, initialDebt: "101.00"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Write(other, "/api/finance/cards/" + Id(card), Card(version: Version(card)), "PUT")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(other, "/api/finance/transactions", Operation(card, null, foreign.Category))).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>();
        var owner = (await db.Accounts.SingleAsync(x => x.Id == Id(s.Account))).UserId;
        db.Cards.Add(new PaymentCard { UserId = owner, Name = "Relación inválida", NormalizedName = "RELACION INVALIDA", Kind = "debit", AccountId = Id(foreign.Account), OpeningDate = new DateOnly(2026,11,10) });
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal("23503", Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [Fact]
    public async Task Database_rejects_second_live_transaction_for_same_occurrence()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101"); var s = await Setup(client);
        var plan = await Create(client, "commitments", Plan(s.Category)); var realized = await Create(client, "commitments/" + Id(plan) + "/confirm", Confirmation(plan, s.Account));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>();
        var row = await db.Transactions.AsNoTracking().SingleAsync(); row.Id = Guid.NewGuid(); db.Transactions.Add(row);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal("23505", Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        Assert.NotNull(realized["commitmentOccurrenceId"]);
    }

    [Fact]
    public async Task Failed_audit_rolls_back_confirmation_occurrence_schedule_and_money()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101"); var s = await Setup(client);
        var plan = await Create(client, "commitments", Plan(s.Category)); var body = Confirmation(plan, s.Account);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>(); var requests = await db.FinancialRequests.CountAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION planning_test_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Injected planning audit failure'; END $$;
            CREATE TRIGGER planning_audit_failure BEFORE INSERT ON "TRANSACTION_REVISIONS" FOR EACH ROW EXECUTE FUNCTION planning_test_failure();
            """);
        try
        {
            var response = await Write(client, "/api/finance/commitments/" + Id(plan) + "/confirm", body); Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.DoesNotContain("Injected", await response.Content.ReadAsStringAsync());
            Assert.Equal(Version(plan), Version(await Get(client, "commitments/" + Id(plan))));
            Assert.Equal(requests, await db.FinancialRequests.CountAsync()); Assert.Empty(await db.Transactions.ToListAsync()); Assert.Empty(await db.CommitmentOccurrences.ToListAsync());
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER planning_audit_failure ON \"TRANSACTION_REVISIONS\"; DROP FUNCTION planning_test_failure();"); }
        Assert.Equal(HttpStatusCode.Created, (await Write(client, "/api/finance/commitments/" + Id(plan) + "/confirm", body)).StatusCode);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("1.001")]
    [InlineData("10000000000000000.00")]
    public async Task Invalid_initial_credit_debt_does_not_create_card(string debt)
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101");
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, "/api/finance/cards", Card(initialDebt: debt))).StatusCode);
        Assert.Empty((await Get(client, "cards")).AsArray());
    }

    [Fact]
    public async Task Debt_above_limit_and_credit_in_favor_are_represented_without_changing_opening()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101"); var s = await Setup(client);
        var card = await Create(client, "cards", Card(initialDebt: "1200.00")); Assert.Equal("-200.00", card["availableCredit"]!.GetValue<string>());
        await Create(client, "transactions", Operation(card, s.Account, null, "card_payment", "1300.00"));
        var current = await Get(client, "cards/" + Id(card)); Assert.Equal("-100.00", current["currentDebt"]!.GetValue<string>()); Assert.Equal("1100.00", current["availableCredit"]!.GetValue<string>());
        Assert.Equal("1200.00", current["initialDebt"]!.GetValue<string>()); Assert.Equal("0.00", (await Get(client, "transactions"))["expense"]!.GetValue<string>());
    }

    [Fact]
    public async Task Confirmation_validation_failure_does_not_advance_or_materialize_schedule()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101"); var s = await Setup(client);
        var plan = await Create(client, "commitments", Plan(s.Category));
        var bad = new { requestId = Guid.NewGuid(), version = Version(plan), dueDate = "2026-11-10", date = "2026-11-09", accountId = Id(s.Account) };
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, "/api/finance/commitments/" + Id(plan) + "/confirm", bad)).StatusCode);
        var unchanged = await Get(client, "commitments/" + Id(plan)); Assert.Equal(Version(plan), Version(unchanged)); Assert.Equal("2026-11-10", unchanged["nextDate"]!.GetValue<string>());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>(); Assert.Empty(await db.CommitmentOccurrences.ToListAsync()); Assert.Empty(await db.Transactions.ToListAsync());
    }

    [Theory]
    [InlineData("weekly", "2027-02-07")]
    [InlineData("fortnightly", "2027-02-15")]
    [InlineData("monthly", "2027-02-28")]
    public void Recurrence_uses_calendar_and_monthly_anchor(string frequency, string expected)
    {
        var first = new DateOnly(2027,1,31); Assert.Equal(DateOnly.Parse(expected), PlanningRules.Next(first, frequency, 31));
        if (frequency == "monthly") Assert.Equal(new DateOnly(2027,3,31), PlanningRules.Next(DateOnly.Parse(expected), frequency, 31));
        var plan = new Commitment { UserId = "synthetic", Name = "Calendario", NormalizedName = "CALENDARIO", Kind = "expense", Frequency = frequency, NextDate = first, AnchorDay = 31 };
        Assert.Single(PlanningRules.Dates(plan, DateOnly.Parse(expected), DateOnly.Parse(expected)));
    }
}
