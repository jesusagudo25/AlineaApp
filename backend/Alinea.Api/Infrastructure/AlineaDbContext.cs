using Alinea.Api.Features.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Alinea.Api.Infrastructure;

public sealed class AlineaDbContext(DbContextOptions<AlineaDbContext> options) : DbContext(options)
{
    public DbSet<GeneralConfiguration> GeneralConfiguration => Set<GeneralConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var config = modelBuilder.Entity<GeneralConfiguration>();
        config.ToTable("CONFIG_GENERAL");
        config.HasKey(x => x.Clave);
        config.Property(x => x.Clave).HasMaxLength(100);
        config.Property(x => x.Valor).HasMaxLength(500).IsRequired();
    }
}
