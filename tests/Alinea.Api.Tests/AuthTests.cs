using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Alinea.Api.Features.Auth;
using Alinea.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Alinea.Api.Tests;

public sealed class TestClock : TimeProvider
{
    private DateTimeOffset now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan time) => now += time;
}

public sealed class AuthFactory : WebApplicationFactory<Program>
{
    public readonly TestClock Clock = new();
    public string InboxKey { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public string Key { get; init; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public string EnvironmentName { get; init; } = "Testing";
    public string Provider { get; init; } = "DevelopmentMailbox";
    public bool Insecure { get; init; }
    public bool FailDelivery { get; init; }
    public string? KeyDirectory { get; init; }
    public HttpMessageHandler? BrevoTransport { get; init; }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = System.Environment.GetEnvironmentVariable("ALINEA_TEST_CONNECTION") ?? throw new InvalidOperationException("PostgreSQL de pruebas requerido."),
            ["Database:ApplyMigrationsOnStartup"] = "true", ["Auth:OtpKey"] = Key,
            ["Sms:Provider"] = Provider, ["Sms:DevelopmentInboxKey"] = InboxKey,
            ["Auth:AllowInsecureLocalHttp"] = Insecure.ToString(),
            ["Sms:Brevo:SendingEnabled"] = (BrevoTransport is not null).ToString(),
            ["Sms:Brevo:ApiKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            ["Sms:Brevo:SenderId"] = "AlineaApp",
        }));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            if (KeyDirectory is not null) services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(KeyDirectory));
            if (FailDelivery) services.AddSingleton<ISmsSender, UnavailableSms>();
            if (BrevoTransport is not null) services.AddHttpClient(BrevoSmsSender.ClientName).ConfigurePrimaryHttpMessageHandler(() => BrevoTransport);
        });
    }
    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>();
        // Solo la base efímera ALINEA_TEST_CONNECTION: nunca la base del piloto.
        await db.FinancialRequests.ExecuteDeleteAsync();
        await db.TransactionRevisions.ExecuteDeleteAsync();
        await db.Transactions.ExecuteDeleteAsync();
        await db.Accounts.ExecuteDeleteAsync(); await db.Categories.ExecuteDeleteAsync(); await db.Banks.ExecuteDeleteAsync();
        await db.AuthSessions.ExecuteDeleteAsync(); await db.Users.ExecuteDeleteAsync();
        await db.OtpChallenges.ExecuteDeleteAsync(); await db.AuthRateBuckets.ExecuteDeleteAsync();
        await db.AuthAudits.ExecuteDeleteAsync(); await db.AllowedPhones.ExecuteDeleteAsync();
        await db.GeneralConfiguration.Where(x => x.Clave == "ACCESO_PROTEGIDO").ExecuteUpdateAsync(s => s.SetProperty(x => x.Valor, "YES"));
    }
    public async Task Admin(string command, string phone)
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AuthAdministration>().ApplyAsync(command, phone, default);
    }
    public async Task Mode(string mode)
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AlineaDbContext>().GeneralConfiguration.Where(x => x.Clave == "ACCESO_PROTEGIDO").ExecuteUpdateAsync(s => s.SetProperty(x => x.Valor, mode));
    }
}

public sealed class UnavailableSms : ISmsSender
{
    public Task<bool> SendAsync(string phone, string code, Guid challengeId, string deviceHash, CancellationToken ct) =>
        throw new InvalidOperationException("Synthetic delivery failure.");
}

public sealed class AuthTests
{
    // Espacio sintético: formato E.164, sin destinatario real ni proveedor externo.
    private const string Phone = "+999000000001";
    private const string OtherPhone = "+999000000002";
    private static async Task<JsonNode> Json(HttpResponseMessage response) => JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object body)
    {
        var csrf = await Json(await client.GetAsync("/api/auth/csrf"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth" + path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf["token"]!.GetValue<string>());
        return await client.SendAsync(request);
    }
    private static async Task<Guid> Request(HttpClient client, string phone = Phone, string purpose = "register")
    {
        var response = await Post(client, "/otp/request", new { phone, purpose });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await Json(response))["challengeId"]!.GetValue<Guid>();
    }
    private static async Task<string?> Code(AuthFactory factory, HttpClient client, Guid id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/development/sms/" + id);
        request.Headers.Add("X-Local-Sms-Inbox", factory.InboxKey);
        var response = await client.SendAsync(request);
        return response.StatusCode == HttpStatusCode.NotFound ? null : (await Json(response))["code"]!.GetValue<string>();
    }
    private static async Task<string> Proof(AuthFactory factory, HttpClient client, string phone = Phone, string purpose = "register")
    {
        var id = await Request(client, phone, purpose);
        var code = await Code(factory, client, id);
        Assert.NotNull(code);
        var response = await Post(client, "/otp/verify", new { challengeId = id, code });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Json(response))["verificationToken"]!.GetValue<string>();
    }
    private static async Task<HttpResponseMessage> Register(AuthFactory factory, HttpClient client, string phone = Phone) =>
        await Post(client, "/register", new { verificationToken = await Proof(factory, client, phone), name = (string?)null, email = (string?)null });

    [Fact]
    public async Task Restricted_registration_cookies_ownership_logout_and_replay()
    {
        await using var factory = new AuthFactory(); using var client = factory.Browser(); await factory.ResetAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
        var denied = await Request(client);
        Assert.Null(await Code(factory, client, denied));
        factory.Clock.Advance(TimeSpan.FromSeconds(61)); await factory.Admin("--allow-phone", Phone);
        var result = await Register(factory, client); Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var cookie = Assert.Single(result.Headers.GetValues("Set-Cookie"), x => x.StartsWith("Alinea.Session="));
        Assert.Contains("httponly", cookie); Assert.Contains("secure", cookie); Assert.Contains("samesite=strict", cookie);
        var session = await Json(await client.GetAsync("/api/auth/session")); Assert.Equal(Phone, session["phone"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/profile/" + session["id"]!.GetValue<string>())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/profile/someone-else")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, "/logout", new { })).StatusCode);
        using var replay = factory.Browser(); replay.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.GetAsync("/api/auth/session")).StatusCode);
    }

    [Fact]
    public async Task Csrf_and_local_inbox_require_tokens_and_same_device()
    {
        await using var factory = new AuthFactory(); using var client = factory.Browser(); await factory.ResetAsync();
        await factory.Admin("--allow-phone", Phone);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/otp/request", new { phone = Phone, purpose = "register" })).StatusCode);
        var id = await Request(client);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/development/sms/" + id)).StatusCode);
        using var stranger = factory.Browser(); await stranger.GetAsync("/api/auth/csrf");
        Assert.Null(await Code(factory, stranger, id));
        Assert.NotNull(await Code(factory, client, id));
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(stranger, "/otp/verify", new { challengeId = id, code = await Code(factory, client, id) })).StatusCode);
    }

    [Fact]
    public async Task Otp_is_hashed_expires_and_locks_after_five_wrong_attempts()
    {
        await using var factory = new AuthFactory(); using var client = factory.Browser(); await factory.ResetAsync(); await factory.Admin("--allow-phone", Phone);
        var id = await Request(client); var code = await Code(factory, client, id); Assert.NotNull(code);
        using (var scope = factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<AlineaDbContext>().OtpChallenges.SingleAsync(x => x.Id == id);
            Assert.Equal(64, row.CodeHash.Length); Assert.NotEqual(code, row.CodeHash);
        }
        var wrong = code == "000000" ? "000001" : "000000";
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/otp/verify", new { challengeId = id, code = wrong })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/otp/verify", new { challengeId = id, code })).StatusCode);
        factory.Clock.Advance(TimeSpan.FromSeconds(61)); id = await Request(client); code = await Code(factory, client, id);
        factory.Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/otp/verify", new { challengeId = id, code })).StatusCode);
    }

    [Fact]
    public async Task Concurrent_verification_and_registration_consume_once()
    {
        await using var factory = new AuthFactory(); using var client = factory.Browser(); await factory.ResetAsync(); await factory.Admin("--allow-phone", Phone);
        var id = await Request(client); var code = await Code(factory, client, id);
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Post(client, "/otp/verify", new { challengeId = id, code })));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(results, x => x.StatusCode == HttpStatusCode.BadRequest);
        var proof = (await Json(results.Single(x => x.StatusCode == HttpStatusCode.OK)))["verificationToken"]!.GetValue<string>();
        var complete = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Post(client, "/register", new { verificationToken = proof })));
        Assert.Single(complete, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(complete, x => x.StatusCode == HttpStatusCode.BadRequest);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>();
        Assert.Single(await db.Users.ToListAsync()); Assert.Single(await db.AuthSessions.ToListAsync());
    }

    [Fact]
    public async Task Registration_rechecks_allowlist_and_config_and_public_mode_still_requires_otp()
    {
        await using var factory = new AuthFactory(); using var client = factory.Browser(); await factory.ResetAsync(); await factory.Admin("--allow-phone", Phone);
        var proof = await Proof(factory, client); await factory.Admin("--revoke-phone", Phone);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/register", new { verificationToken = proof })).StatusCode);
        await factory.Mode("NO");
        Assert.Null(await Code(factory, client, await Request(client, Phone)));
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/register", new { verificationToken = "unverified" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Register(factory, client, OtherPhone)).StatusCode);
        using var other = factory.Browser();
        var publicProof = await Proof(factory, other, "+999000000003"); await factory.Mode("YES");
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(other, "/register", new { verificationToken = publicProof })).StatusCode);
    }

    [Fact]
    public async Task Revocation_blocks_existing_sessions_even_in_public_mode_and_reallow_does_not_enable_account()
    {
        await using var factory = new AuthFactory(); using var first = factory.Browser(); await factory.ResetAsync(); await factory.Admin("--allow-phone", Phone);
        Assert.Equal(HttpStatusCode.OK, (await Register(factory, first)).StatusCode);
        factory.Clock.Advance(TimeSpan.FromSeconds(61)); using var second = factory.Browser();
        Assert.Equal(HttpStatusCode.OK, (await Post(second, "/login", new { verificationToken = await Proof(factory, second, Phone, "login") })).StatusCode);
        factory.Clock.Advance(TimeSpan.FromSeconds(61));
        using var pending = factory.Browser(); var priorProof = await Proof(factory, pending, Phone, "login");
        await factory.Mode("NO"); await factory.Admin("--revoke-phone", Phone);
        foreach (var client in new[] { first, second }) Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
        factory.Clock.Advance(TimeSpan.FromSeconds(61)); await factory.Admin("--allow-phone", Phone);
        Assert.Null(await Code(factory, first, await Request(first, Phone, "login")));
        await factory.Admin("--enable-user", Phone); factory.Clock.Advance(TimeSpan.FromSeconds(61));
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(pending, "/login", new { verificationToken = priorProof })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(first, "/login", new { verificationToken = await Proof(factory, first, Phone, "login") })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/auth/session")).StatusCode);
    }

    [Fact]
    public async Task Cooldown_phone_device_and_ip_limits_are_persistent_and_responses_are_uniform()
    {
        await using var factory = new AuthFactory(); using var client = factory.Browser(); await factory.ResetAsync(); await factory.Admin("--allow-phone", Phone);
        var first = await Request(client); Assert.NotNull(await Code(factory, client, first));
        Assert.Null(await Code(factory, client, await Request(client)));
        factory.Clock.Advance(TimeSpan.FromSeconds(61));
        Assert.NotNull(await Code(factory, client, await Request(client)));
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/otp/verify", new { challengeId = first, code = await Code(factory, client, first) })).StatusCode);
        for (var i = 0; i < 4; i++) { factory.Clock.Advance(TimeSpan.FromSeconds(61)); await Request(client); }
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>();
            Assert.Equal(4, await db.OtpChallenges.CountAsync()); // requests 1,3,4,5; 6+ throttled
            Assert.True(await db.AuthRateBuckets.AnyAsync(x => x.Count >= 6));
        }
        // Different phones still share device/IP rate buckets; blocked requests keep the same 202 schema.
        for (var i = 0; i < 20; i++)
        {
            client.DefaultRequestHeaders.Remove("X-Forwarded-For");
            client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.0.2." + (i + 1));
            await Request(client, "+99900000" + (100 + i));
        }
        using var finalScope = factory.Services.CreateScope(); var finalDb = finalScope.ServiceProvider.GetRequiredService<AlineaDbContext>();
        Assert.True(await finalDb.AuthRateBuckets.AnyAsync(x => x.Id.StartsWith("device:") && x.Count == 11));
        Assert.True(await finalDb.AuthRateBuckets.AnyAsync(x => x.Id.StartsWith("ip:") && x.Count == 21));
        Assert.Equal(1, await finalDb.AuthRateBuckets.CountAsync(x => x.Id.StartsWith("ip:")));
        var count = await finalDb.OtpChallenges.CountAsync();
        await using var restarted = new AuthFactory { Key = factory.Key }; using var fresh = restarted.Browser();
        await Request(fresh);
        Assert.Equal(count, await finalDb.OtpChallenges.CountAsync());
    }

    [Fact]
    public async Task Proof_purpose_device_expiry_and_session_absolute_expiry_are_enforced()
    {
        await using var factory = new AuthFactory(); using var client = factory.Browser(); await factory.ResetAsync(); await factory.Admin("--allow-phone", Phone);
        var proof = await Proof(factory, client);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/login", new { verificationToken = proof })).StatusCode);
        using var stranger = factory.Browser();
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(stranger, "/register", new { verificationToken = proof })).StatusCode);
        factory.Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/register", new { verificationToken = proof })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Register(factory, client)).StatusCode);
        factory.Clock.Advance(TimeSpan.FromMinutes(30));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
    }

    [Fact]
    public async Task Database_failure_with_a_session_keeps_liveness_and_returns_safe_private_failure()
    {
        var directory = Path.Combine(Path.GetTempPath(), "alinea-auth-outage-" + Guid.NewGuid().ToString("N"));
        await using var factory = new AuthFactory { KeyDirectory = directory }; using var browser = factory.Browser();
        await factory.ResetAsync(); await factory.Admin("--allow-phone", Phone);
        var registration = await Register(factory, browser);
        var cookie = Assert.Single(registration.Headers.GetValues("Set-Cookie"), x => x.StartsWith("Alinea.Session=")).Split(';')[0];
        await using var unavailable = new ApiFactory("Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Password=fictitious;Timeout=1", false)
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(factory.Clock);
                services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(directory));
            }));
        using var client = unavailable.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
        var response = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Password", body); Assert.DoesNotContain("Npgsql", body); Assert.DoesNotContain(Phone, body);
    }

    [Fact]
    public async Task Persisted_keys_and_server_session_survive_host_recreation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "alinea-auth-test-" + Guid.NewGuid().ToString("N"));
        string cookie; string key;
        await using (var original = new AuthFactory { KeyDirectory = directory })
        {
            using var browser = original.Browser(); await original.ResetAsync(); await original.Admin("--allow-phone", Phone);
            var result = await Register(original, browser); Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            cookie = Assert.Single(result.Headers.GetValues("Set-Cookie"), x => x.StartsWith("Alinea.Session=")).Split(';')[0];
            key = original.Key;
        }
        await using var restarted = new AuthFactory { KeyDirectory = directory, Key = key }; using var client = restarted.Browser();
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, "/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
    }

    [Fact]
    public async Task Delivery_failure_never_creates_a_verifiable_challenge_or_session()
    {
        await using var factory = new AuthFactory { FailDelivery = true }; using var client = factory.Browser();
        await factory.ResetAsync(); await factory.Admin("--allow-phone", Phone);
        var id = await Request(client); Assert.Null(await Code(factory, client, id));
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/otp/verify", new { challengeId = id, code = "000000" })).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>();
        Assert.False((await db.OtpChallenges.SingleAsync(x => x.Id == id)).Eligible);
        Assert.True(await db.AuthAudits.AnyAsync(x => x.Event == "sms.unavailable"));
        Assert.Empty(await db.AuthSessions.ToListAsync());
    }

    [Theory]
    [InlineData(400, "{\"code\":\"not_enough_credits\"}")]
    [InlineData(402, "{}")]
    [InlineData(401, "{}")]
    [InlineData(429, "{}")]
    [InlineData(503, "{}")]
    [InlineData(201, "{}")]
    public async Task Brevo_failure_blocks_verification_even_with_the_correct_generated_otp(int status, string body)
    {
        using var transport = new RejectedBrevoTransport(status, body);
        await using var factory = new AuthFactory { Provider = "Brevo", BrevoTransport = transport };
        using var client = factory.Browser(); await factory.ResetAsync(); await factory.Admin("--allow-phone", Phone);
        var id = await Request(client);
        Assert.True(transport.SawCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/otp/verify", new { challengeId = id, code = transport.Code })).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AlineaDbContext>();
        var challenge = await db.OtpChallenges.SingleAsync(x => x.Id == id);
        Assert.False(challenge.Eligible); Assert.Null(challenge.GrantHash);
        Assert.True(await db.AuthAudits.AnyAsync(x => x.Event == "sms.unavailable"));
        Assert.Empty(await db.AuthSessions.ToListAsync());
    }

    private sealed class RejectedBrevoTransport(int status, string body) : HttpMessageHandler
    {
        public string Code { get; private set; } = "";
        public bool SawCode => Code.Length == 6;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var payload = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!;
            Code = System.Text.RegularExpressions.Regex.Match(payload["content"]!.GetValue<string>(), @"\b[0-9]{6}\b").Value;
            return new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        }
    }

    [Fact]
    public async Task Disabled_sms_returns_real_unavailability_and_production_has_no_development_inbox()
    {
        await using var factory = new AuthFactory { Provider = "Disabled", EnvironmentName = "Production" };
        using var client = factory.Browser();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Post(client, "/otp/request", new { phone = Phone, purpose = "register" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/development/sms/" + Guid.NewGuid())).StatusCode);
        await using var unsafeFactory = new AuthFactory { EnvironmentName = "Production" };
        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() => unsafeFactory.Browser());
        await using var insecureFactory = new AuthFactory { EnvironmentName = "Production", Provider = "Disabled", Insecure = true };
        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() => insecureFactory.Browser());
    }

    [Theory]
    [InlineData("50700000000", null)]
    [InlineData("+507 0000-0000", "+50700000000")]
    [InlineData("+999１２３４５６７８", null)]
    [InlineData("+012345678", null)]
    public void Phone_normalization_has_ascii_international_rules(string input, string? expected) => Assert.Equal(expected, AuthCrypto.NormalizePhone(input));
}
