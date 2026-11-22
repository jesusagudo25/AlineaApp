using System.Security.Claims;
using Alinea.Api.Features.Auth;
using Alinea.Api.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Alinea.Api.Features.Finance;

public static class CatalogEndpoints
{
    private static string Owner(HttpContext http) => http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static IResult Invalid(string title = "Revisa los datos del formulario.") => Results.Problem(statusCode: 400, title: title);
    private static IResult Conflict(string title) => Results.Problem(statusCode: 409, title: title);
    private static object BankView(Bank row) => new { row.Id, row.Name, row.IsActive, row.Version };
    private static object CategoryView(Category row) => new { row.Id, row.Name, row.Kind, row.IsActive, row.Version, isInitial = row.InitialKey is not null };
    private static object AccountView(FinancialAccount row) => new { row.Id, row.Name, row.Kind, row.BankId, row.Currency,
        openingBalance = CatalogRules.Money(row.OpeningBalance), currentBalance = CatalogRules.Money(row.OpeningBalance), row.OpeningDate,
        openingLocked = row.OpeningLockedAt is not null, row.IsActive, row.Version };

    public static void MapFinancialCatalogs(this WebApplication app)
    {
        var group = app.MapGroup("/api/finance").RequireAuthorization();
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (!http.Request.IsHttps && !http.RequestServices.GetRequiredService<AuthOptions>().AllowInsecureLocalHttp)
                return Invalid("Se requiere HTTPS.");
            if (HttpMethods.IsGet(http.Request.Method)) return await next(context);
            try { await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http); }
            catch (AntiforgeryValidationException) { return Invalid("Solicitud no válida. Actualiza la página."); }
            var db = http.RequestServices.GetRequiredService<AlineaDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(http.RequestAborted);
            try
            {
                var owner = Owner(http);
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({owner}, 202602))", http.RequestAborted);
                var result = await next(context);
                await transaction.CommitAsync(http.RequestAborted);
                return result;
            }
            catch (DbUpdateConcurrencyException) { return Conflict("El registro cambió. Actualiza la lista antes de editar."); }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
            { return Conflict("Ya existe un registro con ese nombre. Revisa también los inactivos."); }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23503" or "23514" })
            { return Conflict("La operación no es compatible con los registros relacionados."); }
        });
        group.MapGet("/banks", async (HttpContext http, AlineaDbContext db, CancellationToken ct) =>
            Results.Ok((await db.Banks.AsNoTracking().Where(x => x.UserId == Owner(http)).OrderBy(x => x.Name).ToListAsync(ct)).Select(BankView)));
        group.MapGet("/banks/{id:guid}", async (Guid id, HttpContext http, AlineaDbContext db, CancellationToken ct) =>
            await db.Banks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == Owner(http), ct) is { } row ? Results.Ok(BankView(row)) : Results.NotFound());
        group.MapPost("/banks", (BankInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct) => SaveBank(null, input, http, db, clock, ct));
        group.MapPut("/banks/{id:guid}", (Guid id, BankInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct) => SaveBank(id, input, http, db, clock, ct));

        group.MapGet("/accounts", async (HttpContext http, AlineaDbContext db, CancellationToken ct) =>
            Results.Ok((await db.Accounts.AsNoTracking().Where(x => x.UserId == Owner(http)).OrderBy(x => x.Name).ToListAsync(ct)).Select(AccountView)));
        group.MapGet("/accounts/{id:guid}", async (Guid id, HttpContext http, AlineaDbContext db, CancellationToken ct) =>
            await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == Owner(http), ct) is { } row ? Results.Ok(AccountView(row)) : Results.NotFound());
        group.MapPost("/accounts", (AccountInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct) => SaveAccount(null, input, http, db, clock, ct));
        group.MapPut("/accounts/{id:guid}", (Guid id, AccountInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct) => SaveAccount(id, input, http, db, clock, ct));

        group.MapGet("/categories", async (HttpContext http, AlineaDbContext db, CancellationToken ct) =>
            Results.Ok((await db.Categories.AsNoTracking().Where(x => x.UserId == Owner(http)).OrderBy(x => x.Kind).ThenBy(x => x.Name).ToListAsync(ct)).Select(CategoryView)));
        group.MapGet("/categories/{id:guid}", async (Guid id, HttpContext http, AlineaDbContext db, CancellationToken ct) =>
            await db.Categories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == Owner(http), ct) is { } row ? Results.Ok(CategoryView(row)) : Results.NotFound());
        group.MapPost("/categories", (CategoryInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct) => SaveCategory(null, input, http, db, clock, ct));
        group.MapPut("/categories/{id:guid}", (Guid id, CategoryInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct) => SaveCategory(id, input, http, db, clock, ct));
        group.MapPost("/categories/initialize", async (HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var owner = Owner(http); var existing = await db.Categories.Where(x => x.UserId == owner).ToListAsync(ct);
            var count = 0;
            foreach (var template in CatalogRules.InitialCategories)
            {
                if (existing.Any(x => x.InitialKey == template.Key || (x.Kind == template.Kind && x.NormalizedName == CatalogRules.Normalize(template.Name)))) continue;
                db.Categories.Add(new Category { UserId = owner, Name = template.Name, NormalizedName = CatalogRules.Normalize(template.Name), Kind = template.Kind,
                    InitialKey = template.Key, CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow() }); count++;
            }
            await db.SaveChangesAsync(ct); return Results.Ok(new { added = count });
        });
    }

    private static async Task<IResult> SaveBank(Guid? id, BankInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var owner = Owner(http);
        var row = id is null ? null : await db.Banks.SingleOrDefaultAsync(x => x.Id == id && x.UserId == owner, ct);
        if (id is not null && row is null) return Results.NotFound();
        if (row is not null && input.Version != row.Version) return Conflict("El banco cambió. Actualiza la lista.");
        var name = CatalogRules.Name(input.Name); if (name is null) return Invalid("El nombre debe tener entre 1 y 100 caracteres válidos.");
        if (row is not null && !input.IsActive && await db.Accounts.AnyAsync(x => x.BankId == row.Id && x.UserId == owner && x.IsActive, ct))
            return Conflict("Desactiva o reasigna las cuentas activas antes de desactivar el banco.");
        if (row is null) { row = new Bank { UserId = owner, Name = name, NormalizedName = CatalogRules.Normalize(name), CreatedAt = clock.GetUtcNow() }; db.Banks.Add(row); }
        row.Name = name; row.NormalizedName = CatalogRules.Normalize(name); row.IsActive = input.IsActive; row.UpdatedAt = clock.GetUtcNow(); row.Version = Guid.NewGuid();
        await db.SaveChangesAsync(ct); return id is null ? Results.Created($"/api/finance/banks/{row.Id}", BankView(row)) : Results.Ok(BankView(row));
    }

    private static async Task<IResult> SaveAccount(Guid? id, AccountInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var owner = Owner(http);
        var row = id is null ? null : await db.Accounts.SingleOrDefaultAsync(x => x.Id == id && x.UserId == owner, ct);
        if (id is not null && row is null) return Results.NotFound();
        if (row is not null && input.Version != row.Version) return Conflict("La cuenta cambió. Actualiza la lista.");
        var name = CatalogRules.Name(input.Name);
        if (name is null || input.Kind is not ("bank" or "cash") || input.Currency != "USD" || input.OpeningDate == default
            || input.OpeningBalance < -CatalogRules.MaxMoney || input.OpeningBalance > CatalogRules.MaxMoney || decimal.Round(input.OpeningBalance, 2) != input.OpeningBalance
            || (input.Kind == "cash" && (input.BankId is not null || input.OpeningBalance < 0))) return Invalid();
        if (input.Kind == "bank")
        {
            var bank = await db.Banks.SingleOrDefaultAsync(x => x.Id == input.BankId && x.UserId == owner, ct);
            if (bank is null || (!bank.IsActive && (input.IsActive || row is null || row.BankId != input.BankId))) return Invalid("Selecciona un banco propio activo.");
        }
        var openingChanged = row is not null && (row.OpeningBalance != input.OpeningBalance || row.OpeningDate != input.OpeningDate);
        if (row?.OpeningLockedAt is not null && (openingChanged || row.Kind != input.Kind || row.BankId != input.BankId))
            return Conflict("La apertura está protegida por movimientos. No puede modificarse.");
        if (openingChanged && !input.ConfirmOpeningChange) return Conflict("Confirma explícitamente el cambio de saldo o fecha de apertura.");
        if (row is null) { row = new FinancialAccount { UserId = owner, Name = name, NormalizedName = CatalogRules.Normalize(name), Kind = input.Kind!, CreatedAt = clock.GetUtcNow() }; db.Accounts.Add(row); }
        row.Name = name; row.NormalizedName = CatalogRules.Normalize(name); row.Kind = input.Kind!; row.BankId = input.BankId;
        row.OpeningBalance = input.OpeningBalance; row.OpeningDate = input.OpeningDate; row.IsActive = input.IsActive; row.UpdatedAt = clock.GetUtcNow(); row.Version = Guid.NewGuid();
        await db.SaveChangesAsync(ct); return id is null ? Results.Created($"/api/finance/accounts/{row.Id}", AccountView(row)) : Results.Ok(AccountView(row));
    }

    private static async Task<IResult> SaveCategory(Guid? id, CategoryInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var owner = Owner(http);
        var row = id is null ? null : await db.Categories.SingleOrDefaultAsync(x => x.Id == id && x.UserId == owner, ct);
        if (id is not null && row is null) return Results.NotFound();
        if (row is not null && input.Version != row.Version) return Conflict("La categoría cambió. Actualiza la lista.");
        var name = CatalogRules.Name(input.Name);
        if (name is null || input.Kind is not ("income" or "expense")) return Invalid();
        if (row is null) { row = new Category { UserId = owner, Name = name, NormalizedName = CatalogRules.Normalize(name), Kind = input.Kind!, CreatedAt = clock.GetUtcNow() }; db.Categories.Add(row); }
        row.Name = name; row.NormalizedName = CatalogRules.Normalize(name); row.Kind = input.Kind!; row.IsActive = input.IsActive; row.Version = Guid.NewGuid(); row.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct); return id is null ? Results.Created($"/api/finance/categories/{row.Id}", CategoryView(row)) : Results.Ok(CategoryView(row));
    }
}
