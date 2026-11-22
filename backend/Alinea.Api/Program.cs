using Alinea.Api.Features.Auth;
using Alinea.Api.Features.Finance;
using Alinea.Api.Features.Health;
using Alinea.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

if (args.Contains("--probe"))
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    try
    {
        using var response = await client.GetAsync("http://127.0.0.1:8080/health/ready");
        Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException) { Environment.ExitCode = 1; }
    catch (TaskCanceledException) { Environment.ExitCode = 1; }
    return;
}

string[] localCommands = ["--migrate", "--allow-phone", "--revoke-phone", "--disable-user", "--enable-user"];
var builder = WebApplication.CreateBuilder(args.Where(x => !localCommands.Contains(x)).ToArray());
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
// El middleware predeterminado también registra excepciones; nuestro handler registra solo tipo y trace.
builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.None);
builder.Services.AddOptions<DatabaseOptions>().BindConfiguration("Database")
    .Validate(DatabaseOptions.IsValid, "Database configuration is missing or invalid; details suppressed.").ValidateOnStart();
builder.Services.AddOptions<CorsOptions>().BindConfiguration("Cors")
    .Validate(CorsOptions.IsValid, "CORS origins must be exact HTTP(S) origins.").ValidateOnStart();
builder.Services.AddDbContext<AlineaDbContext>((services, options) => options.UseNpgsql(
    services.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString,
    postgres => postgres.CommandTimeout(5)));
builder.Services.AddScoped<ReadinessCheck>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SafeExceptionHandler>();
builder.AddAlineaAuth();
builder.Services.AddScoped<AuthAdministration>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (origins.Length > 0) policy.WithOrigins(origins).WithMethods("GET").WithHeaders("Accept");
}));

var app = builder.Build();
// Validar antes de migrar o atender solicitudes; errores sin cadena de conexión.
var databaseOptions = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
_ = app.Services.GetRequiredService<IOptions<CorsOptions>>().Value;
_ = app.Services.GetRequiredService<AuthOptions>();
_ = app.Services.GetRequiredService<SmsOptions>();
if (databaseOptions.ApplyMigrationsOnStartup || args.Contains("--migrate"))
{
    try
    {
        using var scope = app.Services.CreateScope();
        await DatabaseInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<AlineaDbContext>());
        app.Logger.LogInformation("Database migration and non-destructive initialization completed");
    }
    catch (Exception exception)
    {
        app.Logger.LogCritical("Database initialization failed ({ExceptionType}); details suppressed", exception.GetType().Name);
        Environment.ExitCode = 1;
        return;
    }
}
if (args.Contains("--migrate")) return;
var adminCommand = args.SingleOrDefault(x => x is "--allow-phone" or "--revoke-phone" or "--disable-user" or "--enable-user");
if (adminCommand is not null)
{
    using var scope = app.Services.CreateScope();
    var phone = AuthCrypto.NormalizePhone(Environment.GetEnvironmentVariable("ALINEA_ADMIN_PHONE"));
    if (phone is null) { Environment.ExitCode = 1; app.Logger.LogError("Administrative phone input is missing or invalid; input suppressed"); return; }
    await scope.ServiceProvider.GetRequiredService<AuthAdministration>().ApplyAsync(adminCommand, phone, CancellationToken.None);
    app.Logger.LogInformation("Authentication administrative operation completed; subject suppressed");
    return;
}

app.UseExceptionHandler();
if (app.Services.GetRequiredService<AuthOptions>().KnownProxies.Length > 0) app.UseForwardedHeaders();
app.UseStatusCodePages();
app.UseCors();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    await next(context);
});
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "AlineaApp" }));
app.MapAuth();
app.MapFinancialCatalogs();
app.MapGet("/health/ready", async (ReadinessCheck check, CancellationToken cancellationToken) =>
    await check.IsReadyAsync(cancellationToken)
        ? Results.Ok(new { status = "ready" })
        : Results.Json(new { status = "unavailable" }, statusCode: 503));
app.Run();

public partial class Program;
