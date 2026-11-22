using Alinea.Api.Features.Configuration;
using Alinea.Api.Features.Auth;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Alinea.Api.Infrastructure;

public sealed class AlineaDbContext(DbContextOptions<AlineaDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<AllowedPhone> AllowedPhones => Set<AllowedPhone>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<AuthRateBucket> AuthRateBuckets => Set<AuthRateBucket>();
    public DbSet<AuthAudit> AuthAudits => Set<AuthAudit>();
    public DbSet<GeneralConfiguration> GeneralConfiguration => Set<GeneralConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<AppUser>(user =>
        {
            user.Property(x => x.PhoneNumber).HasMaxLength(16);
            user.HasIndex(x => x.PhoneNumber).IsUnique();
            user.Property(x => x.Name).HasMaxLength(120);
        });
        modelBuilder.Entity<AllowedPhone>(phone =>
        {
            phone.ToTable("TELEFONOS_PERMITIDOS"); phone.HasKey(x => x.Phone); phone.Property(x => x.Phone).HasMaxLength(16);
        });
        modelBuilder.Entity<OtpChallenge>(challenge =>
        {
            challenge.ToTable("OTP_CHALLENGES"); challenge.HasKey(x => x.Id);
            challenge.Property(x => x.Phone).HasMaxLength(16);
            challenge.Property(x => x.PhoneHash).HasMaxLength(64); challenge.Property(x => x.DeviceHash).HasMaxLength(64);
            challenge.Property(x => x.CodeHash).HasMaxLength(64); challenge.Property(x => x.GrantHash).HasMaxLength(64);
            challenge.Property(x => x.Purpose).HasMaxLength(10);
            challenge.HasIndex(x => new { x.PhoneHash, x.CreatedAt }); challenge.HasIndex(x => x.ExpiresAt);
        });
        modelBuilder.Entity<AuthSession>(session =>
        {
            session.ToTable("AUTH_SESSIONS"); session.HasKey(x => x.Id);
            session.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            session.HasIndex(x => x.ExpiresAt);
        });
        modelBuilder.Entity<AuthRateBucket>(bucket =>
        {
            bucket.ToTable("AUTH_RATE_BUCKETS"); bucket.HasKey(x => x.Id); bucket.Property(x => x.Id).HasMaxLength(120);
            bucket.HasIndex(x => x.ExpiresAt);
        });
        modelBuilder.Entity<AuthAudit>(audit =>
        {
            audit.ToTable("AUTH_AUDIT"); audit.HasKey(x => x.Id);
            audit.Property(x => x.Event).HasMaxLength(40); audit.Property(x => x.SubjectHash).HasMaxLength(64); audit.HasIndex(x => x.At);
        });
        var config = modelBuilder.Entity<GeneralConfiguration>();
        config.ToTable("CONFIG_GENERAL");
        config.HasKey(x => x.Clave);
        config.Property(x => x.Clave).HasMaxLength(100);
        config.Property(x => x.Valor).HasMaxLength(500).IsRequired();
    }
}
