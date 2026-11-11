using Alinea.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Alinea.Api.Features.Health;

public sealed class ReadinessCheck(AlineaDbContext db)
{
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        try
        {
            if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any()) return false;
            return await db.GeneralConfiguration.CountAsync(x =>
                (x.Clave == "ACCESO_PROTEGIDO" && (x.Valor == "YES" || x.Valor == "NO"))
                || (x.Clave == "MONEDA_PREDETERMINADA" && x.Valor == "USD"), cancellationToken) == 2;
        }
        catch (Exception exception) when (exception is Npgsql.NpgsqlException or TimeoutException or InvalidOperationException)
        {
            // El estado público no revela servidor, credenciales ni SQL.
            return false;
        }
    }
}
