using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Alinea.Api.Features.Finance;

public sealed class PaymentCard : OwnedCatalog
{
    public required string Kind { get; set; }
    public Guid? AccountId { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal InitialDebt { get; set; }
    public DateOnly OpeningDate { get; set; }
    public int? ClosingDay { get; set; }
    public int? PaymentDay { get; set; }
    public bool Used { get; set; }
}
public sealed class Commitment : OwnedCatalog
{
    public required string Kind { get; set; }
    public Guid CategoryId { get; set; }
    public decimal Amount { get; set; }
    public required string Frequency { get; set; }
    public DateOnly? NextDate { get; set; }
    public int AnchorDay { get; set; }
    public string Status { get; set; } = "active";
}
public sealed class CommitmentOccurrence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserId { get; set; }
    public Guid CommitmentId { get; set; }
    public DateOnly DueDate { get; set; }
    public required string Concept { get; set; }
    public required string Kind { get; set; }
    public Guid CategoryId { get; set; }
    public decimal Amount { get; set; }
}
public sealed record CardInput(Guid RequestId, string? Name, string? Kind, Guid? AccountId, DateOnly OpeningDate,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal CreditLimit = 0,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal InitialDebt = 0,
    int? ClosingDay = null, int? PaymentDay = null, bool IsActive = true, Guid? Version = null, bool ConfirmOpeningChange = false);
public sealed record CommitmentInput(Guid RequestId, string? Name, string? Kind, Guid CategoryId,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal Amount,
    string? Frequency, DateOnly? NextDate, string Status = "active", Guid? Version = null);
public sealed record ConfirmCommitmentInput(Guid RequestId, Guid Version, DateOnly DueDate, DateOnly Date, Guid? AccountId, Guid? CardId = null);

public static class PlanningRules
{
    public static bool Money(decimal value, bool positive = true) => value >= (positive ? 0.01m : 0) && value <= CatalogRules.MaxMoney && decimal.Round(value, 2) == value;
    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/Panama")).DateTime);
    public static DateOnly? Next(DateOnly date, string frequency, int anchor)
    {
        try { return frequency switch { "once" => null, "weekly" => date.AddDays(7), "fortnightly" => date.AddDays(15),
            "monthly" => new DateOnly(date.AddMonths(1).Year, date.AddMonths(1).Month, Math.Min(anchor, DateTime.DaysInMonth(date.AddMonths(1).Year, date.AddMonths(1).Month))), _ => null }; }
        catch (ArgumentOutOfRangeException) { return null; }
    }
    public static IEnumerable<DateOnly> Dates(Commitment plan, DateOnly from, DateOnly to)
    {
        var date = plan.NextDate;
        if (date < from && plan.Frequency is "weekly" or "fortnightly")
        { var days = plan.Frequency == "weekly" ? 7 : 15; date = date.Value.AddDays(((from.DayNumber - date.Value.DayNumber) / days) * days); }
        if (date < from && plan.Frequency == "monthly")
        { var months = (from.Year - date.Value.Year) * 12 + from.Month - date.Value.Month; var month = date.Value.AddMonths(months); date = new DateOnly(month.Year, month.Month, Math.Min(plan.AnchorDay, DateTime.DaysInMonth(month.Year, month.Month))); }
        while (date is { } current && current <= to)
        { if (current >= from) yield return current; date = Next(current, plan.Frequency, plan.AnchorDay); }
    }
    public static void Configure(ModelBuilder model)
    {
        var card = model.Entity<PaymentCard>();
        card.ToTable("PAYMENT_CARDS", t => {
            t.HasCheckConstraint("CK_CARDS_Shape", "(\"Kind\"='debit' AND \"AccountId\" IS NOT NULL AND \"CreditLimit\"=0 AND \"InitialDebt\"=0 AND \"ClosingDay\" IS NULL AND \"PaymentDay\" IS NULL) OR (\"Kind\"='credit' AND \"AccountId\" IS NULL AND \"CreditLimit\">0 AND \"InitialDebt\">=0 AND \"ClosingDay\" BETWEEN 1 AND 31 AND \"PaymentDay\" BETWEEN 1 AND 31 AND \"ClosingDay\" IS NOT NULL AND \"PaymentDay\" IS NOT NULL)"); });
        card.HasKey(x => x.Id); card.HasAlternateKey(x => new { x.Id, x.UserId });
        card.Property(x => x.Name).HasMaxLength(100); card.Property(x => x.NormalizedName).HasMaxLength(300); card.Property(x => x.Kind).HasMaxLength(10); card.Property(x => x.Version).IsConcurrencyToken();
        card.Property(x => x.CreditLimit).HasPrecision(18, 2); card.Property(x => x.InitialDebt).HasPrecision(18, 2);
        card.HasIndex(x => new { x.UserId, x.NormalizedName }).IsUnique();
        card.HasOne<Alinea.Api.Features.Auth.AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        card.HasOne<FinancialAccount>().WithMany().HasForeignKey(x => new { x.AccountId, x.UserId }).HasPrincipalKey(x => new { x.Id, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        var plan = model.Entity<Commitment>();
        plan.ToTable("COMMITMENTS", t => { t.HasCheckConstraint("CK_COMMITMENTS_Values", "\"Kind\" IN ('income','expense') AND \"Amount\">0 AND \"Frequency\" IN ('once','weekly','fortnightly','monthly') AND \"Status\" IN ('active','paused','inactive') AND \"AnchorDay\" BETWEEN 1 AND 31"); });
        plan.HasKey(x => x.Id); plan.HasAlternateKey(x => new { x.Id, x.UserId });
        plan.Property(x => x.Name).HasMaxLength(100); plan.Property(x => x.NormalizedName).HasMaxLength(300); plan.Property(x => x.Version).IsConcurrencyToken();
        plan.Property(x => x.Kind).HasMaxLength(10); plan.Property(x => x.Frequency).HasMaxLength(15); plan.Property(x => x.Status).HasMaxLength(10); plan.Property(x => x.Amount).HasPrecision(18, 2);
        plan.HasIndex(x => new { x.UserId, x.NextDate });
        plan.HasOne<Category>().WithMany().HasForeignKey(x => new { x.CategoryId, x.UserId }).HasPrincipalKey(x => new { x.Id, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        var occurrence = model.Entity<CommitmentOccurrence>();
        occurrence.ToTable("COMMITMENT_OCCURRENCES"); occurrence.HasKey(x => x.Id); occurrence.HasAlternateKey(x => new { x.Id, x.UserId });
        occurrence.Property(x => x.Concept).HasMaxLength(100); occurrence.Property(x => x.Kind).HasMaxLength(10); occurrence.Property(x => x.Amount).HasPrecision(18, 2);
        occurrence.HasIndex(x => new { x.CommitmentId, x.DueDate }).IsUnique();
        occurrence.HasOne<Commitment>().WithMany().HasForeignKey(x => new { x.CommitmentId, x.UserId }).HasPrincipalKey(x => new { x.Id, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        occurrence.HasOne<Category>().WithMany().HasForeignKey(x => new { x.CategoryId, x.UserId }).HasPrincipalKey(x => new { x.Id, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        var tx = model.Entity<FinancialTransaction>();
        tx.HasOne<PaymentCard>().WithMany().HasForeignKey(x => new { x.CardId, x.UserId }).HasPrincipalKey(x => new { x.Id, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        tx.HasOne<CommitmentOccurrence>().WithMany().HasForeignKey(x => new { x.CommitmentOccurrenceId, x.UserId }).HasPrincipalKey(x => new { x.Id, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        tx.HasIndex(x => x.CommitmentOccurrenceId).IsUnique().HasFilter("\"CommitmentOccurrenceId\" IS NOT NULL AND NOT \"IsVoided\"");
    }
}
