using System.Net;
using System.Security.Claims;
using Alinea.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Alinea.Api.Features.Auth;

public static class AuthSetup
{
    public const string SessionClaim = "alinea.session";
    public static bool IsLocal(IHostEnvironment environment) => environment.IsDevelopment() || environment.IsEnvironment("Testing");

    public static void AddAlineaAuth(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddOptions<AuthOptions>().BindConfiguration("Auth")
            .Validate(AuthOptions.IsValid, "Auth configuration is missing or invalid; secrets suppressed.")
            .Validate<IHostEnvironment>((o, environment) => !o.AllowInsecureLocalHttp || IsLocal(environment), "Insecure auth cookies are restricted to Development/Testing.")
            .ValidateOnStart();
        builder.Services.AddOptions<SmsOptions>().BindConfiguration("Sms")
            .Validate(o => o.Provider is "Disabled" or "DevelopmentMailbox" or "Brevo", "SMS provider is not configured.")
            .Validate<IHostEnvironment>((o, environment) => o.Provider != "DevelopmentMailbox" || (IsLocal(environment) && o.DevelopmentInboxKey.Length >= 64), "Development SMS requires local environment and a private inbox key.")
            .ValidateOnStart();
        builder.Services.AddSingleton(services => services.GetRequiredService<IOptions<AuthOptions>>().Value);
        builder.Services.AddSingleton(services => services.GetRequiredService<IOptions<SmsOptions>>().Value);
        builder.Services.AddSingleton<AuthCrypto>();
        builder.Services.AddBrevoSms(builder.Configuration);
        builder.Services.AddSingleton<DevelopmentSmsSender>();
        builder.Services.AddSingleton<ISmsSender>(services => services.GetRequiredService<SmsOptions>().Provider switch
        {
            "DevelopmentMailbox" => services.GetRequiredService<DevelopmentSmsSender>(),
            "Brevo" => services.GetRequiredService<BrevoSmsSender>(),
            _ => new DisabledSmsSender()
        });
        builder.Services.AddScoped<AuthService>();
        builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
        {
            options.SignIn.RequireConfirmedPhoneNumber = true;
            options.User.RequireUniqueEmail = false;
        }).AddEntityFrameworkStores<AlineaDbContext>();
        builder.Services.AddAuthorization();
        builder.Services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<AuthOptions, TimeProvider>((options, settings, clock) =>
            {
                options.Cookie.Name = "Alinea.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = settings.AllowInsecureLocalHttp ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                options.SlidingExpiration = false;
                options.TimeProvider = clock;
                options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
                options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
                options.Events.OnValidatePrincipal = ValidateSessionAsync;
            });
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "Alinea.Csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = builder.Configuration.GetValue<bool>("Auth:AllowInsecureLocalHttp") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        var protection = builder.Services.AddDataProtection().SetApplicationName("AlineaApp");
        var keyPath = builder.Configuration["Auth:DataProtectionPath"];
        if (!string.IsNullOrWhiteSpace(keyPath)) protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownProxies.Clear(); options.KnownIPNetworks.Clear();
            foreach (var proxy in builder.Configuration.GetSection("Auth:KnownProxies").Get<string[]>() ?? [])
                if (IPAddress.TryParse(proxy, out var ip)) options.KnownProxies.Add(ip);
        });
        builder.Logging.AddFilter("Microsoft.AspNetCore.Identity", LogLevel.None);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.None);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Antiforgery", LogLevel.None);
    }

    private static async Task ValidateSessionAsync(CookieValidatePrincipalContext context)
    {
        // Las sondas públicas no deben consultar sesiones/BD por una cookie incidental.
        if (context.Request.Path == "/health" || context.Request.Path == "/health/ready")
        {
            context.RejectPrincipal();
            return;
        }
        var services = context.HttpContext.RequestServices;
        var clock = services.GetRequiredService<TimeProvider>();
        var db = services.GetRequiredService<AlineaDbContext>();
        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionId = context.Principal?.FindFirstValue(SessionClaim);
        var session = Guid.TryParse(sessionId, out var id) ? await db.AuthSessions.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId) : null;
        var user = session?.User;
        var revokedPhone = user is not null && await db.AllowedPhones.AnyAsync(x => x.Phone == user.PhoneNumber && !x.Enabled);
        if (session is not null && session.RevokedAt is null && session.ExpiresAt > clock.GetUtcNow()
            && user is { IsDisabled: false, PhoneNumberConfirmed: true } && !revokedPhone
            && AuthCrypto.Equal(user.SecurityStamp, context.Principal?.FindFirstValue("AspNet.Identity.SecurityStamp"))) return;
        if (revokedPhone && user is not null)
            await db.AuthSessions.Where(x => x.UserId == user.Id && x.RevokedAt == null).ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAt, clock.GetUtcNow()));
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }
}
