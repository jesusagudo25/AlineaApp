using Alinea.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Alinea.Api.Features.Auth;

// CLI local: nunca se publica una ruta administrativa ni se registra el teléfono.
public sealed class AuthAdministration(AlineaDbContext db, AuthCrypto crypto, TimeProvider clock)
{
    public async Task ApplyAsync(string command, string phone, CancellationToken ct)
    {
        if (command is not ("--allow-phone" or "--revoke-phone" or "--disable-user" or "--enable-user"))
            throw new ArgumentException("Unknown authentication administrative operation.", nameof(command));
        await using var tx = await AuthService.LockAsync(db, ct);
        var now = clock.GetUtcNow();
        var allowed = await db.AllowedPhones.SingleOrDefaultAsync(x => x.Phone == phone, ct);
        if (command is "--allow-phone" or "--revoke-phone")
        {
            if (allowed is null) { allowed = new AllowedPhone { Phone = phone, CreatedAt = now }; db.AllowedPhones.Add(allowed); }
            allowed.Enabled = command == "--allow-phone";
            allowed.UpdatedAt = now;
        }
        var user = await db.Users.SingleOrDefaultAsync(x => x.PhoneNumber == phone, ct);
        if (command != "--allow-phone")
        {
            var subject = crypto.Hash("phone", phone);
            await db.OtpChallenges.Where(x => x.PhoneHash == subject)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.ConsumedAt, now).SetProperty(x => x.GrantConsumedAt, now), ct);
        }
        if (user is not null && command != "--allow-phone")
        {
            if (command == "--enable-user" && allowed is { Enabled: false })
                throw new InvalidOperationException("A revoked phone cannot be enabled.");
            user.IsDisabled = command != "--enable-user";
            user.SecurityStamp = Guid.NewGuid().ToString();
            await db.AuthSessions.Where(x => x.UserId == user.Id && x.RevokedAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAt, now), ct);
        }
        db.AuthAudits.Add(new AuthAudit { At = now, Event = command[2..], SubjectHash = crypto.Hash("phone", phone) });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
