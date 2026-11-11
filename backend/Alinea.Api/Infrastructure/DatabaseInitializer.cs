using Microsoft.EntityFrameworkCore;

namespace Alinea.Api.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AlineaDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        // No reemplazar valores ya administrados. Una sentencia atómica y clave única.
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "CONFIG_GENERAL" ("Clave", "Valor")
            VALUES ('ACCESO_PROTEGIDO', 'YES'), ('MONEDA_PREDETERMINADA', 'USD')
            ON CONFLICT ("Clave") DO NOTHING;
            """, cancellationToken);
    }
}
