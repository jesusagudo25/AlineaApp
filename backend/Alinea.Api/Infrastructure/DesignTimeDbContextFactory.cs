using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Alinea.Api.Infrastructure;

// Generar/revisar modelos no necesita credenciales ni una BD activa.
// Para aplicar migraciones usar el comando explícito de la aplicación (--migrate).
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AlineaDbContext>
{
    public AlineaDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<AlineaDbContext>()
            .UseNpgsql("Host=localhost;Database=alinea_design")
            .Options);
}
