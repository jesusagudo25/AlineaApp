using System.Security.Claims;
using System.Text.Json;
using Alinea.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Alinea.Api.Features.Finance;

public sealed record PendingCommitment(Guid CommitmentId, Guid Version, DateOnly DueDate, string Concept, string Kind, Guid CategoryId, string Amount);
public static class PlanningEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string Owner(HttpContext http) => http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static IResult Invalid(string title = "Revisa los datos del formulario.") => Results.Problem(statusCode: 400, title: title);
    private static IResult Conflict(string title = "El registro cambió. Actualiza la consulta.") => Results.Problem(statusCode: 409, title: title);
    private static object PlanView(Commitment row) => new { row.Id, row.Name, row.Kind, row.CategoryId, amount = CatalogRules.Money(row.Amount), row.Frequency, row.NextDate, row.Status, row.Version };
    private static async Task<object> CardView(AlineaDbContext db, PaymentCard row, CancellationToken ct)
    {
        var debt = row.InitialDebt + await db.Transactions.Where(x => x.UserId == row.UserId && x.CardId == row.Id && !x.IsVoided).SumAsync(x => x.Kind == "expense" ? x.Amount : -x.Amount, ct);
        return new { row.Id, row.Name, row.Kind, row.AccountId, row.OpeningDate, creditLimit = CatalogRules.Money(row.CreditLimit), initialDebt = CatalogRules.Money(row.InitialDebt),
            currentDebt = row.Kind == "credit" ? CatalogRules.Money(debt) : null, availableCredit = row.Kind == "credit" ? CatalogRules.Money(row.CreditLimit - debt) : null,
            row.ClosingDay, row.PaymentDay, openingLocked = row.Used, row.IsActive, row.Version };
    }
    private static async Task<IResult> Persist(AlineaDbContext db, string owner, Guid key, string hash, object value, int status, CancellationToken ct)
    {
        var response = JsonSerializer.Serialize(value, Json);
        db.FinancialRequests.Add(new FinancialRequest { UserId = owner, RequestId = key, Hash = hash, Response = response, Status = status });
        await db.SaveChangesAsync(ct); return Results.Content(response, "application/json", statusCode: status);
    }
    public static void MapFinancialPlanning(this WebApplication app)
    {
        var group = app.MapGroup("/api/finance").RequireAuthorization(); CatalogEndpoints.Protect(group);
        group.MapGet("/cards", async (HttpContext http, AlineaDbContext db, CancellationToken ct) => {
            var rows = await db.Cards.AsNoTracking().Where(x => x.UserId == Owner(http)).OrderBy(x => x.Name).ToListAsync(ct);
            var views = new List<object>(); foreach (var row in rows) views.Add(await CardView(db, row, ct)); return Results.Ok(views);
        });
        group.MapGet("/cards/{id:guid}", async (Guid id, HttpContext http, AlineaDbContext db, CancellationToken ct) =>
            await db.Cards.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == Owner(http), ct) is { } row ? Results.Ok(await CardView(db, row, ct)) : Results.NotFound());
        group.MapPost("/cards", (CardInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct) => SaveCard(null, input, http, db, clock, ct));
        group.MapPut("/cards/{id:guid}", (Guid id, CardInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct) => SaveCard(id, input, http, db, clock, ct));
        group.MapGet("/commitments", async (HttpContext http, AlineaDbContext db, CancellationToken ct) => Results.Ok((await db.Commitments.AsNoTracking().Where(x => x.UserId == Owner(http)).OrderBy(x => x.Name).ToListAsync(ct)).Select(PlanView)));
        group.MapGet("/commitments/{id:guid}", async (Guid id, HttpContext http, AlineaDbContext db, CancellationToken ct) =>
            await db.Commitments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == Owner(http), ct) is { } row ? Results.Ok(PlanView(row)) : Results.NotFound());
        group.MapPost("/commitments", (CommitmentInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct) => SavePlan(null, input, http, db, clock, ct));
        group.MapPut("/commitments/{id:guid}", (Guid id, CommitmentInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct) => SavePlan(id, input, http, db, clock, ct));
        group.MapGet("/commitments/pending", Pending);
        group.MapPost("/commitments/{id:guid}/confirm", Confirm);
    }
    private static async Task<IResult> SaveCard(Guid? id, CardInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var owner = Owner(http); var hash = TransactionEndpoints.Hash("card/" + id, input);
        if (input.RequestId == Guid.Empty) return Invalid("Falta la clave de solicitud.");
        if (await TransactionEndpoints.Replay(db, owner, input.RequestId, hash, ct) is { } replay) return replay;
        var row = id is null ? null : await db.Cards.SingleOrDefaultAsync(x => x.Id == id && x.UserId == owner, ct);
        if (id is not null && row is null) return Results.NotFound();
        if (row is not null && (row.Version != input.Version || row.Kind != input.Kind)) return Conflict("Actualiza la tarjeta. Su tipo no puede cambiarse.");
        var name = CatalogRules.Name(input.Name);
        if (name is null || input.Kind is not ("debit" or "credit") || input.OpeningDate == default || input.OpeningDate > PlanningRules.Today(clock)) return Invalid();
        if (input.Kind == "debit")
        {
            if (input.CreditLimit != 0 || input.InitialDebt != 0 || input.ClosingDay is not null || input.PaymentDay is not null) return Invalid();
            var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == input.AccountId && x.UserId == owner, ct);
            if (account is null || (!account.IsActive && (input.IsActive || row is null || row.AccountId != input.AccountId)) || input.OpeningDate < account.OpeningDate) return Invalid("Selecciona una cuenta propia activa y respeta su apertura.");
        }
        else if (input.AccountId is not null || !PlanningRules.Money(input.CreditLimit) || !PlanningRules.Money(input.InitialDebt, false)
            || input.ClosingDay is null or < 1 or > 31 || input.PaymentDay is null or < 1 or > 31) return Invalid();
        var openingChanged = row is not null && (row.InitialDebt != input.InitialDebt || row.OpeningDate != input.OpeningDate || row.AccountId != input.AccountId);
        if (openingChanged && (row!.Used || !input.ConfirmOpeningChange)) return Conflict("Confirma el cambio de apertura; una tarjeta utilizada tiene su apertura protegida.");
        if (row is null) { row = new PaymentCard { UserId = owner, Name = name, NormalizedName = CatalogRules.Normalize(name), Kind = input.Kind!, CreatedAt = clock.GetUtcNow() }; db.Cards.Add(row); }
        row.Name = name; row.NormalizedName = CatalogRules.Normalize(name); row.AccountId = input.AccountId; row.OpeningDate = input.OpeningDate; row.CreditLimit = input.CreditLimit; row.InitialDebt = input.InitialDebt;
        row.ClosingDay = input.ClosingDay; row.PaymentDay = input.PaymentDay; row.IsActive = input.IsActive; row.UpdatedAt = clock.GetUtcNow(); row.Version = Guid.NewGuid();
        return await Persist(db, owner, input.RequestId, hash, await CardView(db, row, ct), id is null ? 201 : 200, ct);
    }
    private static async Task<IResult> SavePlan(Guid? id, CommitmentInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var owner = Owner(http); var hash = TransactionEndpoints.Hash("commitment/" + id, input);
        if (input.RequestId == Guid.Empty) return Invalid("Falta la clave de solicitud.");
        if (await TransactionEndpoints.Replay(db, owner, input.RequestId, hash, ct) is { } replay) return replay;
        var row = id is null ? null : await db.Commitments.SingleOrDefaultAsync(x => x.Id == id && x.UserId == owner, ct);
        if (id is not null && row is null) return Results.NotFound();
        if (row is not null && row.Version != input.Version) return Conflict();
        var name = CatalogRules.Name(input.Name);
        if (name is null || input.Kind is not ("income" or "expense") || !PlanningRules.Money(input.Amount) || input.Frequency is not ("once" or "weekly" or "fortnightly" or "monthly")
            || input.Status is not ("active" or "paused" or "inactive") || input.NextDate == default(DateOnly) || (input.NextDate is null && (row is null || row.NextDate is not null || input.Frequency != "once"))) return Invalid();
        var category = await db.Categories.SingleOrDefaultAsync(x => x.Id == input.CategoryId && x.UserId == owner && x.Kind == input.Kind, ct);
        if (category is null || !category.IsActive && (input.Status == "active" || row is null || row.CategoryId != input.CategoryId)) return Invalid("Selecciona una categoría propia activa del mismo tipo.");
        if (row is null) { row = new Commitment { UserId = owner, Name = name, NormalizedName = CatalogRules.Normalize(name), Kind = input.Kind!, Frequency = input.Frequency!, CreatedAt = clock.GetUtcNow() }; db.Commitments.Add(row); }
        if (row.NextDate != input.NextDate || row.Frequency != input.Frequency) row.AnchorDay = input.NextDate?.Day ?? row.AnchorDay;
        row.Name = name; row.NormalizedName = CatalogRules.Normalize(name); row.Kind = input.Kind!; row.CategoryId = input.CategoryId; row.Amount = input.Amount; row.Frequency = input.Frequency!;
        row.NextDate = input.NextDate; row.Status = input.Status; row.IsActive = input.Status == "active"; row.Version = Guid.NewGuid(); row.UpdatedAt = clock.GetUtcNow(); category.UsedByTransactions = true;
        return await Persist(db, owner, input.RequestId, hash, PlanView(row), id is null ? 201 : 200, ct);
    }
    internal static async Task<List<PendingCommitment>> PendingRows(AlineaDbContext db, string owner, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var plans = await db.Commitments.AsNoTracking().Where(x => x.UserId == owner && x.Status == "active").ToListAsync(ct);
        var occurrences = await db.CommitmentOccurrences.AsNoTracking().Where(x => x.UserId == owner && x.DueDate >= from && x.DueDate <= to).ToListAsync(ct);
        var realized = await db.Transactions.Where(x => x.UserId == owner && !x.IsVoided && x.CommitmentOccurrenceId != null).Select(x => x.CommitmentOccurrenceId!.Value).ToListAsync(ct);
        var completed = realized.ToHashSet(); var rows = new List<PendingCommitment>();
        foreach (var plan in plans)
        {
            var previous = occurrences.Where(x => x.CommitmentId == plan.Id).ToDictionary(x => x.DueDate);
            foreach (var date in PlanningRules.Dates(plan, from, to))
            { if (!previous.ContainsKey(date)) rows.Add(new(plan.Id, plan.Version, date, plan.Name, plan.Kind, plan.CategoryId, CatalogRules.Money(plan.Amount))); }
            foreach (var occurrence in previous.Values.Where(x => !completed.Contains(x.Id)))
                rows.Add(new(plan.Id, plan.Version, occurrence.DueDate, occurrence.Concept, occurrence.Kind, occurrence.CategoryId, CatalogRules.Money(occurrence.Amount)));
        }
        return rows.OrderBy(x => x.DueDate).ThenBy(x => x.CommitmentId).ToList();
    }
    private static async Task<IResult> Pending(HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct, DateOnly? from = null, DateOnly? to = null)
    {
        var start = from ?? PlanningRules.Today(clock); var end = to ?? start.AddDays(Math.Min(30, DateOnly.MaxValue.DayNumber - start.DayNumber));
        if (end < start || end.DayNumber - start.DayNumber > 366) return Invalid("Selecciona un período de hasta 366 días.");
        var owner = Owner(http); var rows = await PendingRows(db, owner, start, end, ct);
        var opening = await db.Accounts.Where(x => x.UserId == owner && x.IsActive).SumAsync(x => x.OpeningBalance, ct);
        var activeAccounts = db.Accounts.Where(x => x.UserId == owner && x.IsActive).Select(x => x.Id);
        var actual = opening + await db.Transactions.Where(x => x.UserId == owner && !x.IsVoided && x.AccountId != null && activeAccounts.Contains(x.AccountId.Value)).SumAsync(x => x.Kind == "income" ? x.Amount : -x.Amount, ct)
            + await db.Transactions.Where(x => x.UserId == owner && !x.IsVoided && x.DestinationAccountId != null && activeAccounts.Contains(x.DestinationAccountId.Value)).SumAsync(x => x.Amount, ct);
        var expense = rows.Where(x => x.Kind == "expense").Sum(x => decimal.Parse(x.Amount, System.Globalization.CultureInfo.InvariantCulture));
        return Results.Ok(new { items = rows, from = start, to = end, actualAvailable = CatalogRules.Money(actual), pendingExpenses = CatalogRules.Money(expense), estimatedAvailable = CatalogRules.Money(actual - expense) });
    }
    private static async Task<IResult> Confirm(Guid id, ConfirmCommitmentInput input, HttpContext http, AlineaDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var owner = Owner(http); var hash = TransactionEndpoints.Hash("confirm/" + id, input);
        if (input.RequestId == Guid.Empty || input.DueDate == default) return Invalid();
        if (await TransactionEndpoints.Replay(db, owner, input.RequestId, hash, ct) is { } replay) return replay;
        var plan = await db.Commitments.SingleOrDefaultAsync(x => x.Id == id && x.UserId == owner, ct);
        if (plan is null) return Results.NotFound();
        if (plan.Version != input.Version || plan.Status != "active") return Conflict("Actualiza el compromiso; debe estar activo para confirmarlo.");
        var occurrence = await db.CommitmentOccurrences.SingleOrDefaultAsync(x => x.CommitmentId == id && x.UserId == owner && x.DueDate == input.DueDate, ct);
        if (occurrence is not null && await db.Transactions.AnyAsync(x => x.UserId == owner && x.CommitmentOccurrenceId == occurrence.Id && !x.IsVoided, ct)) return Conflict("Esta ocurrencia ya tiene una transacción vigente.");
        if (occurrence is null)
        {
            if (!PlanningRules.Dates(plan, input.DueDate, input.DueDate).Any()) return Invalid("La fecha no corresponde a una ocurrencia pendiente.");
            occurrence = new CommitmentOccurrence { UserId = owner, CommitmentId = id, DueDate = input.DueDate, Concept = plan.Name, Kind = plan.Kind, CategoryId = plan.CategoryId, Amount = plan.Amount }; db.CommitmentOccurrences.Add(occurrence);
        }
        if (plan.NextDate == input.DueDate)
        {
            var completedDates = await (from o in db.CommitmentOccurrences join t in db.Transactions on o.Id equals t.CommitmentOccurrenceId where o.UserId == owner && o.CommitmentId == id && !t.IsVoided select o.DueDate).ToListAsync(ct);
            plan.NextDate = PlanningRules.Next(input.DueDate, plan.Frequency, plan.AnchorDay);
            while (plan.NextDate is { } next && completedDates.Contains(next)) plan.NextDate = PlanningRules.Next(next, plan.Frequency, plan.AnchorDay);
        }
        plan.Version = Guid.NewGuid(); plan.UpdatedAt = clock.GetUtcNow();
        return await TransactionEndpoints.Save(null, new TransactionInput(input.RequestId, occurrence.Kind, input.AccountId, null, occurrence.CategoryId, occurrence.Amount, input.Date, occurrence.Concept, CardId: input.CardId), http, db, clock, ct, hash, occurrence.Id);
    }
}
