using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Alinea.Api.Features.Finance;

public sealed class FinancialTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserId { get; set; }
    public required string Kind { get; set; }
    public Guid AccountId { get; set; }
    public Guid? DestinationAccountId { get; set; }
    public Guid? CategoryId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string Description { get; set; } = "";
    public bool IsVoided { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class TransactionRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserId { get; set; }
    public Guid TransactionId { get; set; }
    public int Number { get; set; }
    public required string Operation { get; set; }
    public required string Reason { get; set; }
    public required string Snapshot { get; set; }
    public DateTimeOffset At { get; set; }
}
public sealed class FinancialRequest
{
    public required string UserId { get; set; }
    public Guid RequestId { get; set; }
    public required string Hash { get; set; }
    public required string Response { get; set; }
    public int Status { get; set; }
}
public sealed record TransactionInput(Guid RequestId, string? Kind, Guid AccountId, Guid? DestinationAccountId, Guid? CategoryId,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal Amount,
    DateOnly Date, string? Description = null, Guid? Version = null, string? Reason = null);
public sealed record VoidInput(Guid RequestId, Guid Version, string? Reason);

public static class TransactionRules
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<FinancialAccount>().HasAlternateKey(x => new { x.Id, x.UserId });
        model.Entity<Category>().HasAlternateKey(x => new { x.Id, x.UserId });
        var tx = model.Entity<FinancialTransaction>();
        tx.ToTable("TRANSACTIONS", t =>
        {
            t.HasCheckConstraint("CK_TRANSACTIONS_Amount", "\"Amount\" > 0");
            t.HasCheckConstraint("CK_TRANSACTIONS_Shape", "(\"Kind\" IN ('income','expense') AND \"CategoryId\" IS NOT NULL AND \"DestinationAccountId\" IS NULL) OR (\"Kind\" = 'transfer' AND \"CategoryId\" IS NULL AND \"DestinationAccountId\" IS NOT NULL AND \"AccountId\" <> \"DestinationAccountId\")");
        });
        tx.HasKey(x => x.Id); tx.HasAlternateKey(x => new { x.Id, x.UserId });
        tx.Property(x => x.Amount).HasPrecision(18, 2);
        tx.Property(x => x.Kind).HasMaxLength(10); tx.Property(x => x.Description).HasMaxLength(500);
        tx.Property(x => x.Version).IsConcurrencyToken();
        tx.HasIndex(x => new { x.UserId, x.Date, x.Id });
        tx.HasOne<FinancialAccount>().WithMany().HasForeignKey(x => new { x.AccountId, x.UserId }).HasPrincipalKey(x => new { x.Id, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        tx.HasOne<FinancialAccount>().WithMany().HasForeignKey(x => new { x.DestinationAccountId, x.UserId }).HasPrincipalKey(x => new { x.Id, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        tx.HasOne<Category>().WithMany().HasForeignKey(x => new { x.CategoryId, x.UserId }).HasPrincipalKey(x => new { x.Id, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        var revision = model.Entity<TransactionRevision>();
        revision.ToTable("TRANSACTION_REVISIONS"); revision.HasKey(x => x.Id);
        revision.HasIndex(x => new { x.TransactionId, x.Number }).IsUnique();
        revision.Property(x => x.Operation).HasMaxLength(10); revision.Property(x => x.Reason).HasMaxLength(300);
        revision.Property(x => x.Snapshot).HasColumnType("jsonb");
        revision.HasOne<FinancialTransaction>().WithMany().HasForeignKey(x => new { x.TransactionId, x.UserId }).HasPrincipalKey(x => new { x.Id, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        var request = model.Entity<FinancialRequest>();
        request.ToTable("FINANCIAL_REQUESTS"); request.HasKey(x => new { x.UserId, x.RequestId });
        request.Property(x => x.Hash).HasMaxLength(64); request.Property(x => x.Response).HasColumnType("jsonb");
        request.HasOne<Alinea.Api.Features.Auth.AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
