using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Alinea.Api.Features.Auth;
using Microsoft.EntityFrameworkCore;

namespace Alinea.Api.Features.Finance;

public abstract class OwnedCatalog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserId { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid Version { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class Bank : OwnedCatalog;
public sealed class FinancialAccount : OwnedCatalog
{
    public required string Kind { get; set; }
    public Guid? BankId { get; set; }
    public Bank? Bank { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal OpeningBalance { get; set; }
    public DateOnly OpeningDate { get; set; }
    public DateTimeOffset? OpeningLockedAt { get; set; }
}
public sealed class Category : OwnedCatalog
{
    public required string Kind { get; set; }
    public string? InitialKey { get; set; }
}
public sealed record BankInput(string? Name, bool IsActive = true, Guid? Version = null);
public sealed record CategoryInput(string? Name, string? Kind, bool IsActive = true, Guid? Version = null);
public sealed record AccountInput(string? Name, string? Kind, Guid? BankId,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal OpeningBalance,
    DateOnly OpeningDate, string Currency = "USD", bool IsActive = true, Guid? Version = null, bool ConfirmOpeningChange = false);
public sealed record CategoryTemplate(string Key, string Name, string Kind);

public static class CatalogRules
{
    public const decimal MaxMoney = 9999999999999999.99m;
    public static readonly CategoryTemplate[] InitialCategories = [new("salary", "Salario", "income"), new("other-income", "Otros ingresos", "income"), new("food", "Alimentación", "expense"), new("transport", "Transporte", "expense")];
    public static string? Name(string? name)
    {
        if (name is null || name.Length > 300 || name.Any(char.IsControl)) return null;
        var value = Regex.Replace(name.Normalize(NormalizationForm.FormKC).Trim(), @"\s+", " ");
        return value.Length is >= 1 and <= 100 ? value : null;
    }
    public static string Normalize(string name) => name.ToUpperInvariant();
    public static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);
    public static void Configure(ModelBuilder model)
    {
        ConfigureOwned<Bank>(model, "BANKS");
        ConfigureOwned<FinancialAccount>(model, "ACCOUNTS");
        ConfigureOwned<Category>(model, "CATEGORIES");
        var bank = model.Entity<Bank>();
        bank.HasAlternateKey(x => new { x.Id, x.UserId });
        bank.HasIndex(x => new { x.UserId, x.NormalizedName }).IsUnique();
        var account = model.Entity<FinancialAccount>();
        account.HasIndex(x => new { x.UserId, x.NormalizedName }).IsUnique();
        account.Property(x => x.Kind).HasMaxLength(10);
        account.Property(x => x.Currency).HasMaxLength(3);
        account.Property(x => x.OpeningBalance).HasPrecision(18, 2);
        account.HasOne(x => x.Bank).WithMany().HasForeignKey(x => new { x.BankId, x.UserId })
            .HasPrincipalKey(x => new { x.Id, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        account.ToTable("ACCOUNTS", table =>
        {
            table.HasCheckConstraint("CK_ACCOUNTS_KindBank", "(\"Kind\" = 'bank' AND \"BankId\" IS NOT NULL) OR (\"Kind\" = 'cash' AND \"BankId\" IS NULL)");
            table.HasCheckConstraint("CK_ACCOUNTS_Currency", "\"Currency\" = 'USD'");
            table.HasCheckConstraint("CK_ACCOUNTS_CashBalance", "\"Kind\" <> 'cash' OR \"OpeningBalance\" >= 0");
        });
        var category = model.Entity<Category>();
        category.Property(x => x.Kind).HasMaxLength(10);
        category.Property(x => x.InitialKey).HasMaxLength(30);
        category.HasIndex(x => new { x.UserId, x.Kind, x.NormalizedName }).IsUnique();
        category.HasIndex(x => new { x.UserId, x.InitialKey }).IsUnique();
        category.ToTable("CATEGORIES", table => table.HasCheckConstraint("CK_CATEGORIES_Kind", "\"Kind\" IN ('income','expense')"));
    }
    private static void ConfigureOwned<T>(ModelBuilder model, string table) where T : OwnedCatalog
    {
        var entity = model.Entity<T>();
        entity.ToTable(table); entity.HasKey(x => x.Id);
        entity.Property(x => x.Name).HasMaxLength(100);
        entity.Property(x => x.NormalizedName).HasMaxLength(300);
        entity.Property(x => x.Version).IsConcurrencyToken();
        entity.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
