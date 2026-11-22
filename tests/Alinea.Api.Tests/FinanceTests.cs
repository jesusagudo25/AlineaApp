using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Alinea.Api.Features.Finance;
using Alinea.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Alinea.Api.Tests;

public sealed class FinanceTests
{
    private static async Task<JsonNode> Json(HttpResponseMessage response) => JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    private static async Task<HttpResponseMessage> Write(HttpClient client, string path, object body, string method = "POST")
    {
        var csrf = await Json(await client.GetAsync("/api/auth/csrf"));
        using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf["token"]!.GetValue<string>()); return await client.SendAsync(request);
    }
    private static async Task<HttpClient> User(AuthFactory factory, string phone)
    {
        await factory.Admin("--allow-phone", phone); var client = factory.Browser();
        var request = await Write(client, "/api/auth/otp/request", new { phone, purpose = "register" });
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        var id = (await Json(request))["challengeId"]!.GetValue<Guid>();
        using var inboxRequest = new HttpRequestMessage(HttpMethod.Get, "/api/development/sms/" + id);
        inboxRequest.Headers.Add("X-Local-Sms-Inbox", factory.InboxKey);
        var code = (await Json(await client.SendAsync(inboxRequest)))["code"]!.GetValue<string>();
        var proof = await Write(client, "/api/auth/otp/verify", new { challengeId = id, code });
        var result = await Write(client, "/api/auth/register", new { verificationToken = (await Json(proof))["verificationToken"]!.GetValue<string>() });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode); return client;
    }
    private static async Task<JsonNode> Create(HttpClient client, string path, object input)
    {
        var response = await Write(client, "/api/finance/" + path, input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return await Json(response);
    }
    private static object AccountBody(string name, Guid? bank = null, string amount = "120.10", string kind = "bank", string? version = null, bool active = true, bool confirm = false) =>
        new { name, kind, bankId = bank, openingBalance = amount, openingDate = "2026-11-10", currency = "USD", version, isActive = active, confirmOpeningChange = confirm };

    [Fact]
    public async Task Anonymous_and_missing_csrf_are_rejected_and_no_delete_surface_exists()
    {
        await using var factory = new AuthFactory(); using var anonymous = factory.Browser(); await factory.ResetAsync();
        foreach (var path in new[] { "banks", "accounts", "categories" }) Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/finance/" + path)).StatusCode);
        using var user = await User(factory, "+999000000101");
        Assert.Equal(HttpStatusCode.BadRequest, (await user.PostAsJsonAsync("/api/finance/banks", new { name = "Banco ensayo" })).StatusCode);
        var bank = await Create(user, "banks", new { name = "Banco ensayo" });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await user.DeleteAsync("/api/finance/banks/" + bank["id"]!.GetValue<string>())).StatusCode);
    }

    [Fact]
    public async Task Ownership_is_enforced_on_lists_reads_updates_and_bank_association()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync();
        using var first = await User(factory, "+999000000101"); using var second = await User(factory, "+999000000102");
        var bank = await Create(first, "banks", new { name = "Banco privado" });
        var account = await Create(first, "accounts", AccountBody("Cuenta privada", bank["id"]!.GetValue<Guid>()));
        var category = await Create(first, "categories", new { name = "Categoría privada", kind = "expense" });
        foreach (var item in new[] { ("banks", bank), ("accounts", account), ("categories", category) })
        {
            Assert.Empty((await Json(await second.GetAsync("/api/finance/" + item.Item1))).AsArray());
            var path = "/api/finance/" + item.Item1 + "/" + item.Item2["id"]!.GetValue<string>();
            Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync(path)).StatusCode);
            object body = item.Item1 == "accounts" ? AccountBody("No autorizado", bank["id"]!.GetValue<Guid>(), version: item.Item2["version"]!.GetValue<string>()) : new { name = "No autorizado", kind = "expense", version = item.Item2["version"]!.GetValue<string>() };
            Assert.Equal(HttpStatusCode.NotFound, (await Write(second, path, body, "PUT")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(second, "/api/finance/accounts", AccountBody("Ajena", bank["id"]!.GetValue<Guid>()))).StatusCode);
        var secondId = (await Json(await second.GetAsync("/api/auth/session")))["id"]!.GetValue<string>();
        await Create(first, "banks", new { name = "Propietario del servidor", userId = secondId });
        Assert.Empty((await Json(await second.GetAsync("/api/finance/banks"))).AsArray());
        // El mismo nombre sí se permite a otro propietario.
        await Create(second, "banks", new { name = "Banco privado" });
    }

    [Fact]
    public async Task Duplicate_names_are_normalized_persisted_and_atomic_under_concurrency()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101");
        await Create(client, "banks", new { name = "  Banco   Ensayo  " });
        Assert.Equal(HttpStatusCode.Conflict, (await Write(client, "/api/finance/banks", new { name = "banco ensayo" })).StatusCode);
        var concurrent = await Task.WhenAll(Write(client, "/api/finance/banks", new { name = "Banco simultáneo" }), Write(client, "/api/finance/banks", new { name = "BANCO SIMULTÁNEO" }));
        Assert.Single(concurrent, x => x.StatusCode == HttpStatusCode.Created); Assert.Single(concurrent, x => x.StatusCode == HttpStatusCode.Conflict);
        await Create(client, "accounts", AccountBody("Efectivo", amount: "12.34", kind: "cash"));
        Assert.Equal(HttpStatusCode.Conflict, (await Write(client, "/api/finance/accounts", AccountBody("efectivo", kind: "cash"))).StatusCode);
        await Create(client, "categories", new { name = "General", kind = "expense" });
        await Create(client, "categories", new { name = "General", kind = "income" });
        Assert.Equal(HttpStatusCode.Conflict, (await Write(client, "/api/finance/categories", new { name = "general", kind = "expense" })).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>();
        Assert.Equal(2, await db.Banks.CountAsync()); Assert.Equal(12.34m, (await db.Accounts.SingleAsync()).OpeningBalance);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync()); Assert.Equal(3, (await db.Database.GetAppliedMigrationsAsync()).Count());
    }

    [Fact]
    public async Task Bank_and_account_deactivation_preserve_relationships_and_reject_stale_versions()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101");
        var bank = await Create(client, "banks", new { name = "Banco ensayo" }); var bankId = bank["id"]!.GetValue<Guid>(); var bankVersion = bank["version"]!.GetValue<string>();
        var account = await Create(client, "accounts", AccountBody("Cuenta ensayo", bankId));
        var bankPath = "/api/finance/banks/" + bankId;
        Assert.Equal(HttpStatusCode.Conflict, (await Write(client, bankPath, new { name = "Banco ensayo", isActive = false, version = bankVersion }, "PUT")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Write(client, "/api/finance/accounts/" + account["id"]!.GetValue<Guid>(), AccountBody("Cuenta editada", bankId, version: account["version"]!.GetValue<string>(), active: false), "PUT")).StatusCode);
        var inactive = await Write(client, bankPath, new { name = "Banco editado", isActive = false, version = bankVersion }, "PUT");
        Assert.Equal(HttpStatusCode.OK, inactive.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Write(client, bankPath, new { name = "Edición obsoleta", version = bankVersion }, "PUT")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, "/api/finance/accounts", AccountBody("Cuenta no admitida", bankId))).StatusCode);
        Assert.False((await Json(await client.GetAsync(bankPath)))["isActive"]!.GetValue<bool>());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>();
        await Assert.ThrowsAsync<PostgresException>(() => db.Banks.Where(x => x.Id == bankId).ExecuteDeleteAsync());
    }

    [Fact]
    public async Task Opening_balance_precision_confirmation_and_future_movement_lock_are_preserved()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101");
        var account = await Create(client, "accounts", AccountBody("Precisión", amount: "9999999999999999.99", kind: "cash"));
        Assert.Equal("9999999999999999.99", account["openingBalance"]!.GetValue<string>());
        Assert.Equal(account["openingBalance"]!.GetValue<string>(), account["currentBalance"]!.GetValue<string>());
        var path = "/api/finance/accounts/" + account["id"]!.GetValue<Guid>(); var version = account["version"]!.GetValue<string>();
        Assert.Equal(HttpStatusCode.Conflict, (await Write(client, path, AccountBody("Precisión", amount: "80.10", kind: "cash", version: version), "PUT")).StatusCode);
        var changed = await Write(client, path, AccountBody("Precisión", amount: "80.10", kind: "cash", version: version, confirm: true), "PUT");
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode); version = (await Json(changed))["version"]!.GetValue<string>();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>();
        await db.Accounts.Where(x => x.Id == account["id"]!.GetValue<Guid>()).ExecuteUpdateAsync(s => s.SetProperty(x => x.OpeningLockedAt, factory.Clock.GetUtcNow()));
        Assert.Equal(HttpStatusCode.Conflict, (await Write(client, path, AccountBody("Precisión", amount: "90.00", kind: "cash", version: version, confirm: true), "PUT")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Write(client, path, AccountBody("Nuevo nombre", amount: "80.10", kind: "cash", version: version), "PUT")).StatusCode);
        Assert.Equal(80.10m, (await db.Accounts.AsNoTracking().SingleAsync()).OpeningBalance);
    }

    [Theory]
    [InlineData("cash", "-1.00")]
    [InlineData("cash", "1.001")]
    [InlineData("cash", "10000000000000000.00")]
    [InlineData("cash", "-79228162514264337593543950335")]
    [InlineData("card", "1.00")]
    [InlineData("bank", "1.00")]
    public async Task Invalid_money_kind_or_missing_bank_are_rejected(string kind, string amount)
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101");
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, "/api/finance/accounts", AccountBody("Inválida", amount: amount, kind: kind))).StatusCode);
    }

    [Fact]
    public async Task Initial_categories_are_opt_in_idempotent_and_do_not_reset_user_changes()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var client = await User(factory, "+999000000101");
        Assert.Empty((await Json(await client.GetAsync("/api/finance/categories"))).AsArray());
        await Write(client, "/api/finance/categories/initialize", new { });
        var rows = (await Json(await client.GetAsync("/api/finance/categories"))).AsArray(); Assert.Equal(4, rows.Count);
        var row = rows[0]!;
        Assert.Equal(HttpStatusCode.OK, (await Write(client, "/api/finance/categories/" + row["id"]!.GetValue<Guid>(), new { name = "Personalizada desde inicial", kind = row["kind"]!.GetValue<string>(), isActive = false, version = row["version"]!.GetValue<string>() }, "PUT")).StatusCode);
        await Write(client, "/api/finance/categories/initialize", new { });
        var updated = (await Json(await client.GetAsync("/api/finance/categories"))).AsArray(); Assert.Equal(4, updated.Count);
        Assert.Contains(updated, x => x!["name"]!.GetValue<string>() == "Personalizada desde inicial" && !x["isActive"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Composite_foreign_key_rejects_other_users_bank_at_database_level()
    {
        await using var factory = new AuthFactory(); await factory.ResetAsync(); using var first = await User(factory, "+999000000101"); using var second = await User(factory, "+999000000102");
        var bank = await Create(first, "banks", new { name = "Banco propio" });
        var secondId = (await Json(await second.GetAsync("/api/auth/session")))["id"]!.GetValue<string>();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>();
        db.Accounts.Add(new FinancialAccount { UserId = secondId, Name = "Relación rechazada", NormalizedName = "RELACION RECHAZADA", Kind = "bank", BankId = bank["id"]!.GetValue<Guid>(), OpeningDate = new DateOnly(2026, 11, 10), CreatedAt = factory.Clock.GetUtcNow(), UpdatedAt = factory.Clock.GetUtcNow() });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23503", Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }
}
