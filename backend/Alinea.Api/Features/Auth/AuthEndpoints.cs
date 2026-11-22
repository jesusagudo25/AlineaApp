using System.Net.Mail;
using System.Security.Claims;
using Alinea.Api.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Alinea.Api.Features.Auth;

public static class AuthEndpoints
{
    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "El código o la solicitud no son válidos. Solicita uno nuevo.");

    public static string Device(HttpContext context, AuthCrypto crypto, AuthOptions options)
    {
        var token = context.Request.Cookies["Alinea.Device"];
        if (token is null || token.Length != 64 || !token.All(Uri.IsHexDigit))
        {
            token = AuthCrypto.Token();
            context.Response.Cookies.Append("Alinea.Device", token, new CookieOptions
            {
                HttpOnly = true, Secure = !options.AllowInsecureLocalHttp || context.Request.IsHttps,
                SameSite = SameSiteMode.Strict, Path = "/", MaxAge = TimeSpan.FromDays(1), IsEssential = true
            });
        }
        return crypto.Hash("device", token);
    }

    public static void MapAuth(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth");
        auth.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var options = http.RequestServices.GetRequiredService<AuthOptions>();
            if (!http.Request.IsHttps && !options.AllowInsecureLocalHttp)
                return Results.Problem(statusCode: 400, title: "Se requiere HTTPS.");
            if (!HttpMethods.IsGet(http.Request.Method))
            {
                try { await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http); }
                catch (AntiforgeryValidationException) { return Results.Problem(statusCode: 400, title: "Solicitud no válida. Actualiza la página e inténtalo de nuevo."); }
            }
            return await next(context);
        });
        auth.MapGet("/csrf", (HttpContext context, IAntiforgery csrf, AuthCrypto crypto, AuthOptions options) =>
        {
            Device(context, crypto, options);
            return Results.Ok(new { token = csrf.GetAndStoreTokens(context).RequestToken });
        });
        auth.MapGet("/status", (SmsOptions sms, IOptions<BrevoSmsOptions> brevo) => Results.Ok(new { deliveryMode = sms.Provider == "DevelopmentMailbox" ? "local-test" : sms.Provider == "Brevo" && brevo.Value.SendingEnabled ? "sms" : "unavailable", cooldownSeconds = 60 }));
        auth.MapPost("/otp/request", async (OtpRequest input, HttpContext http, AuthService service, AuthCrypto crypto, AuthOptions options, SmsOptions sms, IOptions<BrevoSmsOptions> brevo, CancellationToken ct) =>
        {
            var phone = AuthCrypto.NormalizePhone(input.Phone);
            if (phone is null || input.Purpose is not ("register" or "login")) return Results.Problem(statusCode: 400, title: "Introduce un teléfono en formato internacional válido.");
            if (sms.Provider == "Disabled") return Results.Problem(statusCode: 503, title: "El envío SMS no está configurado.");
            if (sms.Provider == "Brevo" && !brevo.Value.SendingEnabled) return Results.Problem(statusCode: 503, title: "El envío SMS no está habilitado.");
            var id = await service.RequestAsync(phone, input.Purpose, Device(http, crypto, options), crypto.Hash("ip", http.Connection.RemoteIpAddress?.ToString() ?? "unknown"), ct);
            return Results.Json(new { challengeId = id, expiresInSeconds = 300, retryAfterSeconds = 60, message = "Si el número es elegible, recibirás un código." }, statusCode: 202);
        });
        auth.MapPost("/otp/verify", async (OtpVerification input, HttpContext http, AuthService service, AuthCrypto crypto, AuthOptions options, CancellationToken ct) =>
        {
            if (input.Code is null || input.Code.Length != 6 || input.Code.Any(c => c is < '0' or > '9')) return Invalid();
            var proof = await service.VerifyAsync(input.ChallengeId, input.Code, Device(http, crypto, options), crypto.Hash("ip", http.Connection.RemoteIpAddress?.ToString() ?? "unknown"), ct);
            return proof is null ? Invalid() : Results.Ok(new { verificationToken = proof });
        });
        auth.MapPost("/register", async (Registration input, HttpContext http, AuthService service, AuthCrypto crypto, AuthOptions options, SignInManager<AppUser> signIn, CancellationToken ct) =>
        {
            var name = string.IsNullOrWhiteSpace(input.Name) ? null : input.Name.Trim();
            var email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim();
            if (string.IsNullOrWhiteSpace(input.VerificationToken) || name?.Length > 120 || email?.Length > 254
                || (email is not null && (!MailAddress.TryCreate(email, out var address) || address.Address != email)))
                return Results.Problem(statusCode: 400, title: "Revisa los datos del perfil.");
            var session = await service.CompleteAsync(input.VerificationToken, Device(http, crypto, options), "register", name, email, ct);
            return await SignInAsync(session, http, signIn);
        });
        auth.MapPost("/login", async (Login input, HttpContext http, AuthService service, AuthCrypto crypto, AuthOptions options, SignInManager<AppUser> signIn, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(input.VerificationToken)) return Invalid();
            var session = await service.CompleteAsync(input.VerificationToken, Device(http, crypto, options), "login", null, null, ct);
            return await SignInAsync(session, http, signIn);
        });
        auth.MapGet("/session", (HttpContext http, AlineaDbContext db, CancellationToken ct) => ProfileAsync(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!, http, db, ct)).RequireAuthorization();
        auth.MapPost("/logout", async (HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            if (Guid.TryParse(http.User.FindFirstValue(AuthSetup.SessionClaim), out var id))
                await db.AuthSessions.Where(x => x.Id == id && x.UserId == http.User.FindFirstValue(ClaimTypes.NameIdentifier)).ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAt, clock.GetUtcNow()), ct);
            await http.SignOutAsync(IdentityConstants.ApplicationScheme);
            return Results.NoContent();
        }).RequireAuthorization();
        app.MapGet("/api/profile/{userId}", ProfileAsync).RequireAuthorization();
        if (AuthSetup.IsLocal(app.Environment))
            app.MapGet("/api/development/sms/{id:guid}", (Guid id, HttpContext http, SmsOptions sms, AuthOptions options, AuthCrypto crypto, DevelopmentSmsSender sender) =>
            {
                if (sms.Provider != "DevelopmentMailbox" || !AuthCrypto.Equal(sms.DevelopmentInboxKey, http.Request.Headers["X-Local-Sms-Inbox"].ToString())) return Results.NotFound();
                var code = sender.Read(id, Device(http, crypto, options));
                return code is null ? Results.NotFound() : Results.Ok(new { code });
            });
    }

    private static async Task<IResult> SignInAsync(VerifiedSession? session, HttpContext http, SignInManager<AppUser> signIn)
    {
        if (session is null) return Invalid();
        await signIn.SignInWithClaimsAsync(session.User, new AuthenticationProperties { IsPersistent = false, ExpiresUtc = session.ExpiresAt, AllowRefresh = false }, [new Claim(AuthSetup.SessionClaim, session.SessionId.ToString("D"))]);
        return Results.Ok(new { authenticated = true });
    }

    private static async Task<IResult> ProfileAsync(string userId, HttpContext http, AlineaDbContext db, CancellationToken ct)
    {
        if (userId != http.User.FindFirstValue(ClaimTypes.NameIdentifier)) return Results.NotFound();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        return user is null ? Results.NotFound() : Results.Ok(new { id = user.Id, phone = user.PhoneNumber, name = user.Name, email = user.Email });
    }
}
