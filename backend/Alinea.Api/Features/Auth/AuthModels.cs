using Microsoft.AspNetCore.Identity;

namespace Alinea.Api.Features.Auth;

public sealed class AppUser : IdentityUser
{
    public string? Name { get; set; }
    public bool IsDisabled { get; set; }
}

public sealed class AllowedPhone
{
    public required string Phone { get; set; }
    public bool Enabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class OtpChallenge
{
    public Guid Id { get; set; }
    public string? Phone { get; set; }
    public required string PhoneHash { get; set; }
    public required string DeviceHash { get; set; }
    public required string Purpose { get; set; }
    public required string CodeHash { get; set; }
    public bool Eligible { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public string? GrantHash { get; set; }
    public DateTimeOffset? GrantExpiresAt { get; set; }
    public DateTimeOffset? GrantConsumedAt { get; set; }
}

public sealed class AuthSession
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class AuthRateBucket
{
    public required string Id { get; set; }
    public int Count { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class AuthAudit
{
    public long Id { get; set; }
    public DateTimeOffset At { get; set; }
    public required string Event { get; set; }
    public required string SubjectHash { get; set; }
}
