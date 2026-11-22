using System.Globalization;
using System.Security.Cryptography;
using Alinea.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Alinea.Api.Features.Auth;

public sealed record OtpRequest(string Phone, string Purpose);
public sealed record OtpVerification(Guid ChallengeId, string Code);
public sealed record Registration(string VerificationToken, string? Name, string? Email);
public sealed record Login(string VerificationToken);
public sealed record VerifiedSession(AppUser User, Guid SessionId, DateTimeOffset ExpiresAt);

public sealed class AuthService(AlineaDbContext db, UserManager<AppUser> users, AuthCrypto crypto, ISmsSender sms, TimeProvider clock)
{
    public static async Task<IDbContextTransaction> LockAsync(AlineaDbContext context, CancellationToken ct)
    {
        var transaction = await context.Database.BeginTransactionAsync(ct);
        try { await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(73300101)", ct); return transaction; }
        catch { await transaction.DisposeAsync(); throw; }
    }

    private async Task<bool> HitAsync(string scope, string subject, int limit, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var window = now.ToUnixTimeSeconds() / 900;
        var id = scope + ":" + subject + ":" + window.ToString(CultureInfo.InvariantCulture);
        var bucket = await db.AuthRateBuckets.FindAsync([id], ct);
        if (bucket is null)
        {
            bucket = new() { Id = id, ExpiresAt = DateTimeOffset.FromUnixTimeSeconds((window + 1) * 900) };
            db.AuthRateBuckets.Add(bucket);
        }
        bucket.Count = Math.Min(bucket.Count + 1, limit + 1);
        return bucket.Count <= limit;
    }

    private void Audit(string name, string subject) => db.AuthAudits.Add(new() { At = clock.GetUtcNow(), Event = name, SubjectHash = subject });

    private async Task CommitAsync(IDbContextTransaction transaction, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<bool> RegistrationAllowedAsync(string phone, bool lockRows, CancellationToken ct)
    {
        var settings = lockRows
            ? await db.GeneralConfiguration.FromSqlRaw("SELECT * FROM \"CONFIG_GENERAL\" WHERE \"Clave\" = 'ACCESO_PROTEGIDO' FOR SHARE").ToListAsync(ct)
            : await db.GeneralConfiguration.Where(x => x.Clave == "ACCESO_PROTEGIDO").ToListAsync(ct);
        var mode = settings.SingleOrDefault()?.Valor;
        // Falla cerrado ante ausencia o valor desconocido.
        var allowed = lockRows
            ? await db.AllowedPhones.FromSqlInterpolated($"SELECT * FROM \"TELEFONOS_PERMITIDOS\" WHERE \"Phone\" = {phone} FOR SHARE").ToListAsync(ct)
            : await db.AllowedPhones.Where(x => x.Phone == phone).ToListAsync(ct);
        if (allowed.SingleOrDefault()?.Enabled == false) return false;
        return mode == "NO" || (mode == "YES" && allowed.SingleOrDefault()?.Enabled == true);
    }

    public async Task<Guid> RequestAsync(string phone, string purpose, string deviceHash, string ipHash, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var phoneHash = crypto.Hash("phone", phone);
        var id = Guid.NewGuid();
        await using var transaction = await LockAsync(db, ct);
        // Retención de estado efímero: no se elimina configuración, usuarios o datos de negocio.
        await db.AuthRateBuckets.Where(x => x.ExpiresAt < now).ExecuteDeleteAsync(ct);
        await db.OtpChallenges.Where(x => x.ExpiresAt < now.AddDays(-1)).ExecuteDeleteAsync(ct);
        await db.AuthAudits.Where(x => x.At < now.AddDays(-30)).ExecuteDeleteAsync(ct);
        await db.AuthSessions.Where(x => x.ExpiresAt < now.AddDays(-1)).ExecuteDeleteAsync(ct);
        var phoneLimit = await HitAsync("phone", phoneHash, 5, ct);
        var ipLimit = await HitAsync("ip", ipHash, 20, ct);
        var deviceLimit = await HitAsync("device", deviceHash, 10, ct);
        var cooldown = await db.OtpChallenges.AnyAsync(x => x.PhoneHash == phoneHash && x.CreatedAt > now.AddSeconds(-60), ct);
        if (!phoneLimit || !ipLimit || !deviceLimit || cooldown)
        {
            Audit("otp.request.throttled", phoneHash);
            await CommitAsync(transaction, ct);
            return id; // Identificador señuelo; misma respuesta y sin detalles del límite.
        }
        var user = await db.Users.SingleOrDefaultAsync(x => x.PhoneNumber == phone, ct);
        var allowed = await RegistrationAllowedAsync(phone, false, ct);
        var revoked = await db.AllowedPhones.AnyAsync(x => x.Phone == phone && !x.Enabled, ct);
        var eligible = purpose == "register" ? user is null && allowed : !revoked && user is { PhoneNumberConfirmed: true, IsDisabled: false };
        await db.OtpChallenges.Where(x => x.PhoneHash == phoneHash && x.ConsumedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.ConsumedAt, now), ct);
        var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var challenge = new OtpChallenge
        {
            Id = id, Phone = eligible ? phone : null, PhoneHash = phoneHash, DeviceHash = deviceHash,
            Purpose = purpose, CodeHash = crypto.Hash("code", id.ToString("D") + ":" + code),
            Eligible = eligible, CreatedAt = now, ExpiresAt = now.AddMinutes(5)
        };
        db.OtpChallenges.Add(challenge);
        if (eligible)
        {
            try { challenge.Eligible = await sms.SendAsync(phone, code, id, deviceHash, ct); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                challenge.Eligible = false;
            }
            if (!challenge.Eligible) Audit("sms.unavailable", phoneHash);
        }
        Audit("otp.request.accepted", phoneHash);
        await CommitAsync(transaction, ct);
        return id;
    }

    public async Task<string?> VerifyAsync(Guid id, string code, string deviceHash, string ipHash, CancellationToken ct)
    {
        await using var transaction = await LockAsync(db, ct);
        var ipAllowed = await HitAsync("verify-ip", ipHash, 100, ct);
        var deviceAllowed = await HitAsync("verify-device", deviceHash, 50, ct);
        var challenge = await db.OtpChallenges.FindAsync([id], ct);
        var now = clock.GetUtcNow();
        string? token = null;
        if (ipAllowed && deviceAllowed && challenge is not null && AuthCrypto.Equal(challenge.DeviceHash, deviceHash)
            && challenge.ConsumedAt is null && challenge.Attempts < 5 && challenge.ExpiresAt > now)
        {
            challenge.Attempts++;
            var correct = AuthCrypto.Equal(challenge.CodeHash, crypto.Hash("code", id.ToString("D") + ":" + code));
            if (challenge.Eligible && correct)
            {
                token = id.ToString("D") + "." + AuthCrypto.Token();
                challenge.ConsumedAt = now;
                challenge.GrantHash = crypto.Hash("grant", token);
                challenge.GrantExpiresAt = now.AddMinutes(5);
            }
            else if (challenge.Attempts >= 5) challenge.ConsumedAt = now;
        }
        Audit(token is null ? "otp.verify.rejected" : "otp.verify.accepted", challenge?.PhoneHash ?? deviceHash);
        await CommitAsync(transaction, ct);
        return token;
    }

    private async Task<OtpChallenge?> GrantAsync(string token, string deviceHash, string purpose, CancellationToken ct)
    {
        if (token.Length > 150 || !Guid.TryParse(token.Split('.')[0], out var id)) return null;
        var challenge = await db.OtpChallenges.FindAsync([id], ct);
        return challenge is not null && challenge.Eligible && challenge.ConsumedAt is not null && challenge.Phone is not null
            && challenge.Purpose == purpose && challenge.GrantConsumedAt is null && challenge.GrantExpiresAt > clock.GetUtcNow()
            && AuthCrypto.Equal(challenge.DeviceHash, deviceHash) && AuthCrypto.Equal(challenge.GrantHash, crypto.Hash("grant", token))
            ? challenge : null;
    }

    public async Task<VerifiedSession?> CompleteAsync(string token, string deviceHash, string purpose, string? name, string? email, CancellationToken ct)
    {
        await using var transaction = await LockAsync(db, ct);
        var challenge = await GrantAsync(token, deviceHash, purpose, ct);
        if (challenge is null) { await CommitAsync(transaction, ct); return null; }
        challenge.GrantConsumedAt = clock.GetUtcNow();
        var user = await db.Users.SingleOrDefaultAsync(x => x.PhoneNumber == challenge.Phone, ct);
        if (purpose == "register")
        {
            if (user is not null || !await RegistrationAllowedAsync(challenge.Phone!, true, ct))
            {
                Audit("registration.rejected", challenge.PhoneHash); await CommitAsync(transaction, ct); return null;
            }
            user = new AppUser { UserName = challenge.Phone, PhoneNumber = challenge.Phone, PhoneNumberConfirmed = true, Name = name, Email = email };
            if (!(await users.CreateAsync(user)).Succeeded)
            {
                Audit("registration.rejected", challenge.PhoneHash); await CommitAsync(transaction, ct); return null;
            }
        }
        if (user is null || user.IsDisabled || !user.PhoneNumberConfirmed
            || await db.AllowedPhones.AnyAsync(x => x.Phone == user.PhoneNumber && !x.Enabled, ct))
        {
            await CommitAsync(transaction, ct); return null;
        }
        var session = new AuthSession { Id = Guid.NewGuid(), UserId = user.Id, CreatedAt = clock.GetUtcNow(), ExpiresAt = clock.GetUtcNow().AddMinutes(30) };
        db.AuthSessions.Add(session);
        Audit(purpose == "register" ? "registration.completed" : "login.completed", challenge.PhoneHash);
        await CommitAsync(transaction, ct);
        return new(user, session.Id, session.ExpiresAt);
    }
}
