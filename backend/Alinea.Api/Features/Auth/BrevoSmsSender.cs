using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Alinea.Api.Features.Auth;

public sealed class BrevoSmsOptions
{
    public bool SendingEnabled { get; set; }
    public string ApiKey { get; set; } = "";
    public string SenderId { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 3;

    public static bool IsValid(BrevoSmsOptions options) => options.TimeoutSeconds is >= 1 and <= 5
        && (!options.SendingEnabled || (options.ApiKey.Length is > 0 and <= 1024
            && options.ApiKey.All(c => c is >= '!' and <= '~')
            && Regex.IsMatch(options.SenderId, @"\A(?:[A-Za-z0-9]{3,11}|[0-9]{12,15})\z", RegexOptions.CultureInvariant)));
}

public static class BrevoSmsRegistration
{
    public static void AddBrevoSms(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BrevoSmsOptions>().Bind(configuration.GetSection("Sms:Brevo"))
            .Validate(BrevoSmsOptions.IsValid, "Brevo SMS configuration is invalid; details suppressed.").ValidateOnStart();
        services.AddHttpClient(BrevoSmsSender.ClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })
            .RedactLoggedHeaders(_ => true).RemoveAllLoggers();
        services.AddSingleton<BrevoSmsSender>();
    }
}

public sealed class BrevoSmsSender(IHttpClientFactory clients, IOptions<BrevoSmsOptions> options, ILogger<BrevoSmsSender> logger) : ISmsSender
{
    public const string ClientName = "Alinea.BrevoSms";
    private static readonly Uri Endpoint = new("https://api.brevo.com/v3/transactionalSMS/send");

    public async Task<bool> SendAsync(string phone, string code, Guid challengeId, string deviceHash, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        // Seleccionar Brevo no autoriza por sí mismo a enviar mensajes reales.
        if (!settings.SendingEnabled || !BrevoSmsOptions.IsValid(settings)) return false;
        var recipient = AuthCrypto.NormalizePhone(phone);
        if (recipient is null || code.Length != 6 || code.Any(c => c is < '0' or > '9')) return false;
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        try
        {
            using var client = clients.CreateClient(ClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = JsonContent.Create(new
                {
                    sender = settings.SenderId, recipient,
                    content = $"AlineaApp: Tu codigo de acceso es {code}. Vence en 5 minutos. No lo compartas.",
                    type = "transactional", unicodeEnabled = false
                })
            };
            request.Headers.Add("api-key", settings.ApiKey);
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            // No seguir redirecciones, reintentar ni revelar mensajes del proveedor.
            using var payload = await ReadPayloadAsync(response.Content, timeout.Token);
            if (response.StatusCode != HttpStatusCode.Created)
            {
                var credits = response.StatusCode == HttpStatusCode.PaymentRequired
                    || (payload?.RootElement.ValueKind == JsonValueKind.Object
                        && payload.RootElement.TryGetProperty("code", out var error)
                        && error.ValueKind == JsonValueKind.String && error.GetString() == "not_enough_credits");
                logger.LogWarning("Brevo SMS rejected: category {Category}, HTTP {StatusCode}; details suppressed.", credits ? "credits" : "http", (int)response.StatusCode);
                return false;
            }
            var accepted = payload?.RootElement.ValueKind == JsonValueKind.Object
                && payload.RootElement.TryGetProperty("messageId", out var id) && id.ValueKind == JsonValueKind.Number
                && id.TryGetInt64(out var number) && number > 0;
            if (!accepted) logger.LogWarning("Brevo SMS acceptance was invalid; details suppressed.");
            return accepted;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Brevo SMS timed out; details suppressed.");
            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            logger.LogWarning("Brevo SMS transport failed; details suppressed.");
            return false;
        }
    }

    private static async Task<JsonDocument?> ReadPayloadAsync(HttpContent content, CancellationToken ct)
    {
        // Limitar también el cuerpo y su tiempo de lectura; nunca registrar su contenido.
        const int limit = 4096;
        if (content.Headers.ContentLength > limit) return null;
        await using var stream = await content.ReadAsStreamAsync(ct);
        var buffer = new byte[limit + 1];
        var count = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, ct);
        if (count > limit) return null;
        try { return JsonDocument.Parse(buffer.AsMemory(0, count)); }
        catch (JsonException) { return null; }
    }
}
