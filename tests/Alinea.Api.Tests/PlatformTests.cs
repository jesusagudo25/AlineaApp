using System.Net;
using System.Net.Http.Json;
using Alinea.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alinea.Api.Tests;

public sealed class ApiFactory(string connection, bool migrate) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = connection,
            ["Database:ApplyMigrationsOnStartup"] = migrate.ToString(),
            ["Cors:AllowedOrigins:0"] = "http://localhost:5173"
        }));
    }
}

public sealed class PlatformTests
{
    private static string TestConnection => Environment.GetEnvironmentVariable("ALINEA_TEST_CONNECTION")
        ?? throw new InvalidOperationException("Use Compose tests: PostgreSQL de pruebas es obligatorio.");

    [Fact]
    public async Task Migration_seed_restart_and_existing_values_are_preserved()
    {
        await using var factory = new ApiFactory(TestConnection, true);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Single(await db.Database.GetAppliedMigrationsAsync());
        await DatabaseInitializer.InitializeAsync(db);
        await DatabaseInitializer.InitializeAsync(db);
        var values = await db.GeneralConfiguration.AsNoTracking().ToDictionaryAsync(x => x.Clave, x => x.Valor);
        Assert.Equal(2, values.Count);
        Assert.Equal("YES", values["ACCESO_PROTEGIDO"]);
        Assert.Equal("USD", values["MONEDA_PREDETERMINADA"]);
        var access = await db.GeneralConfiguration.SingleAsync(x => x.Clave == "ACCESO_PROTEGIDO");
        access.Valor = "NO";
        await db.SaveChangesAsync();
        try
        {
            await DatabaseInitializer.InitializeAsync(db);
            await using var restarted = new ApiFactory(TestConnection, true);
            using var restartedClient = restarted.CreateClient();
            Assert.Equal(HttpStatusCode.OK, (await restartedClient.GetAsync("/health/ready")).StatusCode);
            await db.Entry(access).ReloadAsync();
            Assert.Equal("NO", access.Valor);
            Assert.Equal(2, await db.GeneralConfiguration.CountAsync());
        }
        finally { access.Valor = "YES"; await db.SaveChangesAsync(); }
    }

    [Fact]
    public async Task Database_outage_does_not_break_liveness_or_leak_details()
    {
        await using var factory = new ApiFactory("Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Password=not-a-secret;Timeout=1", false);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("{\"status\":\"unavailable\"}", await response.Content.ReadAsStringAsync());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Public_surface_has_no_config_write_or_fake_login_and_rejects_unknown_origins()
    {
        await using var factory = new ApiFactory(TestConnection, false);
        using var client = factory.CreateClient();
        foreach (var path in new[] { "/configuration", "/config", "/api/config", "/login", "/register" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(path, new { value = "NO" })).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PostAsJsonAsync("/health", new { })).StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://untrusted.invalid");
        using var response = await client.SendAsync(request);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        using var allowed = new HttpRequestMessage(HttpMethod.Get, "/health");
        allowed.Headers.Add("Origin", "http://localhost:5173");
        using var allowedResponse = await client.SendAsync(allowed);
        Assert.Equal("http://localhost:5173", Assert.Single(allowedResponse.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.False(allowedResponse.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-connection")]
    [InlineData("Host=localhost;Database=test;Username=test")]
    [InlineData("Host=localhost;Database=test;Username=test;Password=fictitious;Include Error Detail=true")]
    public void Invalid_database_options_are_rejected(string connection) => Assert.False(DatabaseOptions.IsValid(new() { ConnectionString = connection }));

    [Theory]
    [InlineData("*")]
    [InlineData("https://example.invalid/path")]
    [InlineData("https://user@example.invalid")]
    public void Invalid_cors_options_are_rejected(string origin) => Assert.False(CorsOptions.IsValid(new() { AllowedOrigins = [origin] }));
}
