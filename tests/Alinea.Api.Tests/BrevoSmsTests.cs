using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Alinea.Api.Features.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Alinea.Api.Tests;

// No sockets: el transporte se sustituye siempre antes de resolver ISmsSender.
public sealed class BrevoSmsTests
{
    private const string Phone = "+999000000001";
    private const string Code = "000000";
    private static BrevoSmsOptions Settings() => new()
    {
        SendingEnabled = true, ApiKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), SenderId = "AlineaApp", TimeoutSeconds = 1
    };

    [Fact]
    public async Task Factory_sends_only_official_transactional_payload_and_requires_acceptance()
    {
        var settings = Settings();
        using var handler = new StubHandler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://api.brevo.com/v3/transactionalSMS/send", request.RequestUri!.AbsoluteUri);
            Assert.Equal(settings.ApiKey, Assert.Single(request.Headers.GetValues("api-key")));
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal(Phone, body.RootElement.GetProperty("recipient").GetString());
            Assert.Equal("AlineaApp", body.RootElement.GetProperty("sender").GetString());
            Assert.Equal("transactional", body.RootElement.GetProperty("type").GetString());
            var text = body.RootElement.GetProperty("content").GetString()!;
            Assert.Contains(Code, text); Assert.Contains("5 minutos", text); Assert.True(text.Length < 160);
            Assert.All(text, c => Assert.InRange((int)c, 32, 126));
            Assert.DoesNotContain("STOP", text); Assert.False(body.RootElement.GetProperty("unicodeEnabled").GetBoolean());
            Assert.False(body.RootElement.TryGetProperty("webUrl", out _));
            return Response(201, "{\"messageId\":1511882900176220}");
        });
        using var services = Provider(settings, handler);
        Assert.True(await services.GetRequiredService<BrevoSmsSender>().SendAsync(Phone, Code, Guid.NewGuid(), "device", default));
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(400, "{\"code\":\"not_enough_credits\"}")]
    [InlineData(402, "{\"message\":\"Insufficient credits\"}")]
    [InlineData(401, "{\"code\":\"unauthorized\"}")]
    [InlineData(403, "{\"code\":\"permission_denied\"}")]
    [InlineData(429, "{\"code\":\"rate_limit\"}")]
    [InlineData(500, "unavailable")]
    [InlineData(503, "unavailable")]
    [InlineData(302, "{\"messageId\":1}")]
    [InlineData(200, "{\"messageId\":1}")]
    [InlineData(202, "{\"messageId\":1}")]
    [InlineData(201, "not-json")]
    [InlineData(201, "{}")]
    [InlineData(201, "null")]
    [InlineData(201, "{\"messageId\":0}")]
    [InlineData(201, "{\"messageId\":-1}")]
    [InlineData(201, "{\"messageId\":\"1\"}")]
    public async Task Http_or_invalid_acceptance_fails_closed_without_retry(int status, string body)
    {
        using var handler = new StubHandler((_, _) => Task.FromResult(Response(status, body)));
        using var services = Provider(Settings(), handler);
        Assert.False(await services.GetRequiredService<BrevoSmsSender>().SendAsync(Phone, Code, Guid.NewGuid(), "device", default));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Disabled_gate_makes_no_http_call_even_with_configured_key()
    {
        var settings = Settings(); settings.SendingEnabled = false;
        using var handler = new StubHandler((_, _) => throw new InvalidOperationException("Transport must not run."));
        using var services = Provider(settings, handler);
        Assert.False(await services.GetRequiredService<BrevoSmsSender>().SendAsync(Phone, Code, Guid.NewGuid(), "device", default));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Transport_timeout_and_read_timeout_fail_closed_without_retry()
    {
        foreach (var duringRead in new[] { false, true })
        {
            using var handler = new StubHandler(async (_, ct) =>
            {
                if (!duringRead) await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StreamContent(new WaitingStream()) };
            });
            using var services = Provider(Settings(), handler);
            Assert.False(await services.GetRequiredService<BrevoSmsSender>().SendAsync(Phone, Code, Guid.NewGuid(), "device", default));
            Assert.Equal(1, handler.Calls);
        }
    }

    [Fact]
    public async Task Caller_cancellation_propagates_without_turning_into_acceptance()
    {
        using var handler = new StubHandler(async (_, ct) => { await Task.Delay(Timeout.InfiniteTimeSpan, ct); return Response(201, "{}"); });
        using var services = Provider(Settings(), handler); using var cancellation = new CancellationTokenSource();
        var pending = services.GetRequiredService<BrevoSmsSender>().SendAsync(Phone, Code, Guid.NewGuid(), "device", cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Logs_never_include_api_key_phone_otp_provider_body_or_exception_message()
    {
        var settings = Settings(); var logger = new CaptureLogger();
        var sensitive = settings.ApiKey + Phone + Code + "provider-private-detail";
        using var handler = new StubHandler((_, _) => Task.FromResult(Response(400, JsonSerializer.Serialize(new { code = "not_enough_credits", message = sensitive }))));
        using var services = Provider(settings, handler, logger);
        Assert.False(await services.GetRequiredService<BrevoSmsSender>().SendAsync(Phone, Code, Guid.NewGuid(), "device", default));
        Assert.Contains("credits", logger.Text);
        using var failing = new StubHandler((_, _) => throw new HttpRequestException(sensitive));
        using var failedServices = Provider(settings, failing, logger);
        Assert.False(await failedServices.GetRequiredService<BrevoSmsSender>().SendAsync(Phone, Code, Guid.NewGuid(), "device", default));
        Assert.DoesNotContain(settings.ApiKey, logger.Text); Assert.DoesNotContain(Phone, logger.Text);
        Assert.DoesNotContain(Code, logger.Text); Assert.DoesNotContain("provider-private-detail", logger.Text);
    }

    [Fact]
    public async Task Oversized_success_response_is_not_accepted()
    {
        using var handler = new StubHandler((_, _) => Task.FromResult(Response(201, "{\"messageId\":1,\"padding\":\"" + new string('x', 5000) + "\"}")));
        using var services = Provider(Settings(), handler);
        Assert.False(await services.GetRequiredService<BrevoSmsSender>().SendAsync(Phone, Code, Guid.NewGuid(), "device", default));
    }

    [Theory]
    [InlineData("", "AlineaApp", 3)]
    [InlineData("key\r\ninjected", "AlineaApp", 3)]
    [InlineData("key", "sender with spaces", 3)]
    [InlineData("key", "AlineaApp", 0)]
    [InlineData("key", "AlineaApp", 6)]
    public void Invalid_active_configuration_is_rejected_without_echoing_input(string key, string sender, int timeout)
    {
        var settings = new BrevoSmsOptions { SendingEnabled = true, ApiKey = key, SenderId = sender, TimeoutSeconds = timeout };
        Assert.False(BrevoSmsOptions.IsValid(settings));
        using var handler = new StubHandler((_, _) => throw new InvalidOperationException("No HTTP allowed."));
        using var services = Provider(settings, handler);
        var error = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<BrevoSmsOptions>>().Value);
        Assert.Equal("Brevo SMS configuration is invalid; details suppressed.", Assert.Single(error.Failures));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public void Provider_selection_uses_existing_abstraction_and_local_fake_stays_restricted()
    {
        foreach (var provider in new[] { "Brevo", "Disabled", "DevelopmentMailbox" })
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:OtpKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                ["Sms:Provider"] = provider, ["Sms:DevelopmentInboxKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
            });
            builder.AddAlineaAuth();
            using var services = builder.Services.BuildServiceProvider();
            var sender = services.GetRequiredService<ISmsSender>();
            Assert.Equal(provider == "Brevo" ? typeof(BrevoSmsSender) : provider == "Disabled" ? typeof(DisabledSmsSender) : typeof(DevelopmentSmsSender), sender.GetType());
        }
        var production = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        production.Configuration.Sources.Clear();
        production.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Sms:Provider"] = "DevelopmentMailbox", ["Sms:DevelopmentInboxKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) });
        production.AddAlineaAuth(); using var unsafeServices = production.Services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => unsafeServices.GetRequiredService<ISmsSender>());
    }

    private static ServiceProvider Provider(BrevoSmsOptions settings, HttpMessageHandler handler, ILogger<BrevoSmsSender>? logger = null)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sms:Brevo:SendingEnabled"] = settings.SendingEnabled.ToString(), ["Sms:Brevo:ApiKey"] = settings.ApiKey,
            ["Sms:Brevo:SenderId"] = settings.SenderId, ["Sms:Brevo:TimeoutSeconds"] = settings.TimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
        }).Build();
        services.AddBrevoSms(configuration);
        services.AddSingleton(logger ?? NullLogger<BrevoSmsSender>.Instance);
        services.AddHttpClient(BrevoSmsSender.ClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    private static HttpResponseMessage Response(int status, string body) => new((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Calls++; return response(request, cancellationToken); }
    }
    private sealed class CaptureLogger : ILogger<BrevoSmsSender>
    {
        public string Text { get; private set; } = "";
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Text += formatter(state, exception);
    }
    private sealed class WaitingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return 0; }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

public sealed class BrevoSmsGateTests
{
    [Fact]
    public async Task Disabled_sending_reports_unavailable_and_returns_503_without_database_or_provider_calls()
    {
        await using var factory = new GateFactory(); using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var status = JsonDocument.Parse(await client.GetStringAsync("/api/auth/status"));
        Assert.Equal("unavailable", status.RootElement.GetProperty("deliveryMode").GetString());
        using var csrf = JsonDocument.Parse(await client.GetStringAsync("/api/auth/csrf"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/otp/request") { Content = JsonContent.Create(new { phone = "+999000000001", purpose = "register" }) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.RootElement.GetProperty("token").GetString());
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(0, factory.TransportCalls);
    }

    private sealed class GateFactory : WebApplicationFactory<Program>
    {
        public int TransportCalls { get; private set; }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Host=127.0.0.1;Port=1;Database=unused;Username=test;Password=fictitious;Timeout=1",
                ["Database:ApplyMigrationsOnStartup"] = "false", ["Auth:OtpKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                ["Sms:Provider"] = "Brevo", ["Sms:Brevo:SendingEnabled"] = "false", ["Sms:Brevo:ApiKey"] = "", ["Sms:Brevo:SenderId"] = ""
            }));
            builder.ConfigureServices(services => services.AddHttpClient(BrevoSmsSender.ClientName).ConfigurePrimaryHttpMessageHandler(() => new DenyHandler(() => TransportCalls++)));
        }
        private sealed class DenyHandler(Action called) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { called(); throw new InvalidOperationException("No network allowed."); }
        }
    }
}
