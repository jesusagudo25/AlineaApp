using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Alinea.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Alinea.Api.Features.Finance;

public static class TransactionEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string Owner(HttpContext http) => http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static IResult Invalid(string title = "Revisa importe, fecha, cuentas y categoría.") => Results.Problem(statusCode: 400, title: title);
    private static IResult Conflict(string title) => Results.Problem(statusCode: 409, title: title);
    private static object View(FinancialTransaction row) => new { row.Id, row.Kind, row.AccountId, row.DestinationAccountId, row.CategoryId,
        amount = CatalogRules.Money(row.Amount), currency = "USD", row.Date, row.Description, row.IsVoided, row.Version, row.CreatedAt, row.UpdatedAt };
    public static Task<decimal> Balance(AlineaDbContext db, FinancialAccount account, CancellationToken ct) =>
        db.Accounts.Where(a => a.Id == account.Id && a.UserId == account.UserId).Select(a => a.OpeningBalance
            + db.Transactions.Where(t => t.UserId == a.UserId && !t.IsVoided && t.AccountId == a.Id).Sum(t => t.Kind == "income" ? t.Amount : -t.Amount)
            + db.Transactions.Where(t => t.UserId == a.UserId && !t.IsVoided && t.DestinationAccountId == a.Id).Sum(t => t.Amount)).SingleAsync(ct);

    public static void MapFinancialTransactions(this WebApplication app)
    {
        var group = app.MapGroup("/api/finance/transactions").RequireAuthorization();
        CatalogEndpoints.Protect(group);
        group.MapGet("", List);
        group.MapGet("/{id:guid}", async (Guid id, HttpContext http, AlineaDbContext db, CancellationToken ct) =>
        {
            var row = await db.Transactions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == Owner(http), ct);
            if (row is null) return Results.NotFound();
            var revisions = await db.TransactionRevisions.AsNoTracking().Where(x => x.TransactionId == id && x.UserId == Owner(http)).OrderBy(x => x.Number)
                .Select(x => new { x.Number, x.Operation, x.Reason, x.At, x.Snapshot }).ToListAsync(ct);
            return Results.Ok(new { transaction = View(row), revisions });
        });
        group.MapPost("", (TransactionInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct) => Save(null, input, http, db, clock, ct));
        group.MapPut("/{id:guid}", (Guid id, TransactionInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct) => Save(id, input, http, db, clock, ct));
        group.MapPost("/{id:guid}/void", Void);
    }
    private static async Task<IResult> List(HttpContext http, AlineaDbContext db, CancellationToken ct,
        DateOnly? from = null, DateOnly? to = null, Guid? accountId = null, string? kind = null, Guid? categoryId = null, int page = 1, int pageSize = 20)
    {
        if (page < 1 || page > 1000000 || pageSize is < 1 or > 100 || from > to || kind is not (null or "income" or "expense" or "transfer")) return Invalid("Filtros o paginación no válidos.");
        var query = db.Transactions.AsNoTracking().Where(x => x.UserId == Owner(http));
        if (from is not null) query = query.Where(x => x.Date >= from);
        if (to is not null) query = query.Where(x => x.Date <= to);
        if (accountId is not null) query = query.Where(x => x.AccountId == accountId || x.DestinationAccountId == accountId);
        if (kind is not null) query = query.Where(x => x.Kind == kind);
        if (categoryId is not null) query = query.Where(x => x.CategoryId == categoryId);
        var count = await query.CountAsync(ct);
        var income = await query.Where(x => !x.IsVoided && x.Kind == "income").SumAsync(x => x.Amount, ct);
        var expense = await query.Where(x => !x.IsVoided && x.Kind == "expense").SumAsync(x => x.Amount, ct);
        var rows = await query.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Results.Ok(new { items = rows.Select(View), total = count, page, pageSize, income = CatalogRules.Money(income), expense = CatalogRules.Money(expense) });
    }
    private static string Hash(string operation, object body) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(operation + JsonSerializer.Serialize(body, Json))));
    private static async Task<IResult?> Replay(AlineaDbContext db, string owner, Guid key, string hash, CancellationToken ct)
    {
        var previous = await db.FinancialRequests.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == owner && x.RequestId == key, ct);
        return previous is null ? null : previous.Hash == hash ? Results.Content(previous.Response, "application/json", statusCode: previous.Status)
            : Conflict("La clave de solicitud ya se utilizó con otros datos.");
    }
    private static async Task<IResult> Record(AlineaDbContext db, FinancialTransaction row, Guid key, string hash, string operation, string reason, TimeProvider clock, CancellationToken ct)
    {
        row.Version = Guid.NewGuid(); row.UpdatedAt = clock.GetUtcNow();
        var response = JsonSerializer.Serialize(View(row), Json); var status = operation == "create" ? 201 : 200;
        var number = await db.TransactionRevisions.CountAsync(x => x.TransactionId == row.Id && x.UserId == row.UserId, ct) + 1;
        db.TransactionRevisions.Add(new TransactionRevision { Number = number, UserId = row.UserId, TransactionId = row.Id, Operation = operation, Reason = reason, Snapshot = response, At = clock.GetUtcNow() });
        db.FinancialRequests.Add(new FinancialRequest { UserId = row.UserId, RequestId = key, Hash = hash, Response = response, Status = status });
        await db.SaveChangesAsync(ct);
        return Results.Content(response, "application/json", statusCode: status);
    }
    private static async Task<IResult> Save(Guid? id, TransactionInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var owner = Owner(http); var hash = Hash("save/" + id, input);
        if (input.RequestId == Guid.Empty) return Invalid("Se requiere una clave de solicitud.");
        if (await Replay(db, owner, input.RequestId, hash, ct) is { } replay) return replay;
        var row = id is null ? null : await db.Transactions.SingleOrDefaultAsync(x => x.Id == id && x.UserId == owner, ct);
        if (id is not null && row is null) return Results.NotFound();
        if (row is not null && (row.IsVoided || input.Version != row.Version)) return Conflict("El movimiento cambió o está anulado. Actualiza el historial.");
        var reason = CatalogRules.Name(input.Reason);
        if (id is not null && reason is null) return Invalid("Indica un motivo de edición (hasta 100 caracteres).");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/Panama")).DateTime);
        if (input.Kind is not ("income" or "expense" or "transfer") || input.Amount <= 0 || input.Amount > CatalogRules.MaxMoney || decimal.Round(input.Amount, 2) != input.Amount
            || input.Date == default || input.Date > today || input.Description?.Length > 500 || input.Description?.Any(char.IsControl) == true) return Invalid();
        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == input.AccountId && x.UserId == owner && x.IsActive, ct);
        if (account is null || input.Date < account.OpeningDate) return Invalid("Selecciona una cuenta propia activa y una fecha desde su apertura.");
        FinancialAccount? destination = null;
        if (input.Kind == "transfer")
        {
            if (input.CategoryId is not null || input.DestinationAccountId == input.AccountId) return Invalid();
            destination = await db.Accounts.SingleOrDefaultAsync(x => x.Id == input.DestinationAccountId && x.UserId == owner && x.IsActive, ct);
            if (destination is null || input.Date < destination.OpeningDate) return Invalid("Selecciona otra cuenta propia activa y respeta su apertura.");
        }
        else if (input.DestinationAccountId is not null || !await db.Categories.AnyAsync(x => x.Id == input.CategoryId && x.UserId == owner && x.IsActive && x.Kind == input.Kind, ct)) return Invalid("Selecciona una categoría propia activa del mismo tipo.");
        if (input.CategoryId is not null)
        {
            var usedCategory = await db.Categories.SingleAsync(x => x.Id == input.CategoryId && x.UserId == owner, ct);
            usedCategory.UsedByTransactions = true;
        }
        if (row is null) { row = new FinancialTransaction { UserId = owner, Kind = input.Kind!, CreatedAt = clock.GetUtcNow() }; db.Transactions.Add(row); }
        // Account opening locks are permanent, including accounts used by previous revisions.
        foreach (var used in new[] { account, destination }.OfType<FinancialAccount>())
        { used.OpeningLockedAt ??= clock.GetUtcNow(); used.Version = Guid.NewGuid(); used.UpdatedAt = clock.GetUtcNow(); }
        row.Kind = input.Kind!; row.AccountId = input.AccountId; row.DestinationAccountId = input.DestinationAccountId; row.CategoryId = input.CategoryId;
        row.Amount = input.Amount; row.Date = input.Date; row.Description = input.Description?.Trim() ?? "";
        return await Record(db, row, input.RequestId, hash, id is null ? "create" : "edit", reason ?? "Registro inicial", clock, ct);
    }
    private static async Task<IResult> Void(Guid id, VoidInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var owner = Owner(http); var hash = Hash("void/" + id, input);
        if (input.RequestId == Guid.Empty) return Invalid("Se requiere una clave de solicitud.");
        if (await Replay(db, owner, input.RequestId, hash, ct) is { } replay) return replay;
        var row = await db.Transactions.SingleOrDefaultAsync(x => x.Id == id && x.UserId == owner, ct);
        if (row is null) return Results.NotFound();
        if (row.IsVoided || row.Version != input.Version) return Conflict("El movimiento cambió o ya está anulado.");
        var reason = CatalogRules.Name(input.Reason); if (reason is null) return Invalid("Indica un motivo de anulación (hasta 100 caracteres).");
        row.IsVoided = true;
        return await Record(db, row, input.RequestId, hash, "void", reason, clock, ct);
    }
}
