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

var builder = WebApplication.CreateBuilder(args);
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
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (origins.Length > 0) policy.WithOrigins(origins).WithMethods("GET").WithHeaders("Accept");
}));

var app = builder.Build();
// Validar antes de migrar o atender solicitudes; errores sin cadena de conexión.
var databaseOptions = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
_ = app.Services.GetRequiredService<IOptions<CorsOptions>>().Value;
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

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    await next(context);
});
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "AlineaApp" }));
app.MapGet("/health/ready", async (ReadinessCheck check, CancellationToken cancellationToken) =>
    await check.IsReadyAsync(cancellationToken)
        ? Results.Ok(new { status = "ready" })
        : Results.Json(new { status = "unavailable" }, statusCode: 503));
app.Run();

public partial class Program;
