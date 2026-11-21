using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Common;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Common;
using RestaurantCms.Domain.Tables;

namespace RestaurantCms.Application.Features.Tables;

internal sealed class TableFieldsRules : Validator<TableFields>
{
    public TableFieldsRules()
    {
        Required("code", x => x.Code);
        MaxLength("code", x => x.Code, 12);
        Required("label", x => x.Label);
        MaxLength("label", x => x.Label, 40);
        Range("seats", x => x.Seats, 1, 40);
        Rule("gridX", x => x.GridX >= 0 && x.GridY >= 0, "Floor position cannot be negative.");
    }
}

public static class CreateTable
{
    public sealed record Command(TableFields Fields) : ICommand<TableDto>;

    internal sealed class Validator : Validator<Command>
    {
        public Validator() => Include(x => x.Fields, new TableFieldsRules());
    }

    internal sealed class Handler(ITableRepository tables, ITenantRepository tenants, ITenantContext tenant, IUnitOfWork uow)
        : ICommandHandler<Command, TableDto>
    {
        public async Task<Result<TableDto>> Handle(Command command, CancellationToken ct)
        {
            var f = command.Fields;
            var plan = (await tenants.GetAsync(tenant.TenantId, ct))?.Limits;
            if (plan is not null && await tables.CountAsync(ct) >= plan.MaxTables)
                return Error.Conflict("Plan.TableLimit", $"Your plan allows {plan.MaxTables} tables. Upgrade to add more.");
            if (await tables.CodeExistsAsync(f.Code, null, ct))
                return Error.Conflict("Table.DuplicateCode", $"Table code '{f.Code}' is already used.");

            var created = DiningTable.Create(f.Code, f.Label, f.Seats, f.Area, f.GridX, f.GridY);
            if (created.IsFailure) return created.Error!;
            tables.Add(created.Value);
            await uow.SaveChangesAsync(ct);
            return created.Value.ToDto();
        }
    }
}

public static class UpdateTable
{
    public sealed record Command(Guid Id, TableFields Fields) : ICommand<TableDto>;

    internal sealed class Validator : Validator<Command>
    {
        public Validator() => Include(x => x.Fields, new TableFieldsRules());
    }

    internal sealed class Handler(ITableRepository tables, TableViews views, IUnitOfWork uow, IClock clock) : ICommandHandler<Command, TableDto>
    {
        public async Task<Result<TableDto>> Handle(Command command, CancellationToken ct)
        {
            var table = await tables.GetAsync(command.Id, ct);
            if (table is null) return Error.NotFound("Table", command.Id);
            var f = command.Fields;
            if (await tables.CodeExistsAsync(f.Code, command.Id, ct))
                return Error.Conflict("Table.DuplicateCode", $"Table code '{f.Code}' is already used.");
            var updated = table.Update(f.Code, f.Label, f.Seats, f.Area, f.GridX, f.GridY, f.IsActive);
            if (updated.IsFailure) return updated.Error!;
            table.Touch(clock.UtcNow);
            await uow.SaveChangesAsync(ct);
            return (await views.DetailAsync(table, ct)).Table;
        }
    }
}

public static class DeleteTable
{
    public sealed record Command(Guid Id) : ICommand<Unit>;

    internal sealed class Handler(ITableRepository tables, IUnitOfWork uow) : ICommandHandler<Command, Unit>
    {
        public async Task<Result<Unit>> Handle(Command command, CancellationToken ct)
        {
            var table = await tables.GetAsync(command.Id, ct);
            if (table is null) return Error.NotFound("Table", command.Id);
            if (table.CurrentSessionId.HasValue)
                return Error.Conflict("Table.InUse", "Settle the table's current visit before deleting it.");
            tables.Remove(table);
            await uow.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}

/// <summary>Seats guests at a table (opens a visit).</summary>
public static class SeatTable
{
    public sealed record Command(Guid TableId, int Guests, string? WaiterName) : ICommand<TableDetailDto>;

    internal sealed class Validator : Validator<Command>
    {
        public Validator() => Range("guests", x => x.Guests, 1, 40);
    }

    internal sealed class Handler(ITableRepository tables, TableViews views, IUnitOfWork uow, IClock clock) : ICommandHandler<Command, TableDetailDto>
    {
        public async Task<Result<TableDetailDto>> Handle(Command command, CancellationToken ct)
        {
            var table = await tables.GetAsync(command.TableId, ct);
            if (table is null) return Error.NotFound("Table", command.TableId);
            var opened = table.OpenSession(command.Guests, command.WaiterName, clock.UtcNow);
            if (opened.IsFailure) return opened.Error!;
            tables.AddSession(opened.Value);
            await uow.SaveChangesAsync(ct);
            return await views.DetailAsync(table, ct);
        }
    }
}

/// <summary>Settles the bill: completes served orders, closes the visit, table goes to cleaning.</summary>
public static class SettleTable
{
    public sealed record Command(Guid TableId) : ICommand<TableDetailDto>;

    internal sealed class Handler(ITableRepository tables, IOrderRepository orders, IUnitOfWork uow, IClock clock)
        : ICommandHandler<Command, TableDetailDto>
    {
        public async Task<Result<TableDetailDto>> Handle(Command command, CancellationToken ct)
        {
            var table = await tables.GetAsync(command.TableId, ct);
            if (table is null) return Error.NotFound("Table", command.TableId);
            if (table.CurrentSessionId is not { } sid)
                return Error.Conflict("Table.NoSession", $"{table.Label} has no open visit.");
            var session = await tables.GetSessionAsync(sid, ct);
            if (session is null) return Error.NotFound("TableSession", sid);

            var now = clock.UtcNow;
            var sessionOrders = await orders.GetBySessionAsync(sid, ct);
            var closed = table.CloseSession(session, sessionOrders.Any(o => o.IsOpen), now);
            if (closed.IsFailure) return closed.Error!;
            foreach (var order in sessionOrders.Where(o => o.Status == OrderStatus.Served))
                order.ChangeStatus(OrderStatus.Completed, now);

            await uow.SaveChangesAsync(ct);
            // The final bill of the visit that was just closed, for printing.
            return new TableDetailDto(table.ToDto(), session.ToDto(),
                sessionOrders.Select(o => Orders.OrderMapping.ToDto(o)).ToList(), sessionOrders.ToBill());
        }
    }
}

public static class MarkTableClean
{
    public sealed record Command(Guid TableId) : ICommand<TableDto>;

    internal sealed class Handler(ITableRepository tables, IUnitOfWork uow, IClock clock) : ICommandHandler<Command, TableDto>
    {
        public async Task<Result<TableDto>> Handle(Command command, CancellationToken ct)
        {
            var table = await tables.GetAsync(command.TableId, ct);
            if (table is null) return Error.NotFound("Table", command.TableId);
            var result = table.MarkClean(clock.UtcNow);
            if (result.IsFailure) return result.Error!;
            await uow.SaveChangesAsync(ct);
            return table.ToDto();
        }
    }
}
