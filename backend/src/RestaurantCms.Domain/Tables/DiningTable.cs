using System.Text.RegularExpressions;
using RestaurantCms.Domain.Common;

namespace RestaurantCms.Domain.Tables;

public sealed partial class DiningTable : TenantEntity
{
    private DiningTable() { }

    /// <summary>Short public code used in the QR URL, e.g. "T12" → /t/T12.</summary>
    public string Code { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public int Seats { get; private set; }
    public string Area { get; private set; } = "Indoor";
    public TableStatus Status { get; private set; } = TableStatus.Free;
    public int GridX { get; private set; }
    public int GridY { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid? CurrentSessionId { get; private set; }

    [GeneratedRegex("^[A-Za-z0-9-]{1,12}$")]
    private static partial Regex CodePattern();

    public static Result<DiningTable> Create(string code, string label, int seats, string area, int gridX = 0, int gridY = 0)
    {
        var table = new DiningTable();
        var result = table.Update(code, label, seats, area, gridX, gridY, true);
        return result.IsFailure ? result.Error! : table;
    }

    public Result Update(string code, string label, int seats, string area, int gridX, int gridY, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(code) || !CodePattern().IsMatch(code.Trim()))
            return Error.Validation("Table.Code", "Table code must be 1–12 letters, numbers or dashes.");
        if (string.IsNullOrWhiteSpace(label))
            return Error.Validation("Table.Label", "Table label is required.");
        if (seats is < 1 or > 40)
            return Error.Validation("Table.Seats", "Seats must be between 1 and 40.");
        if (gridX < 0 || gridY < 0)
            return Error.Validation("Table.Position", "Floor position cannot be negative.");
        if (!isActive && Status == TableStatus.Occupied)
            return Error.Conflict("Table.InUse", "Close the current session before deactivating this table.");

        Code = code.Trim().ToUpperInvariant();
        Label = label.Trim();
        Seats = seats;
        Area = string.IsNullOrWhiteSpace(area) ? "Indoor" : area.Trim();
        GridX = gridX;
        GridY = gridY;
        IsActive = isActive;
        if (Id != Guid.Empty) Changed();
        return Result.Success();
    }

    public Result EnsureCanTakeOrder()
    {
        if (!IsActive)
            return Error.Conflict("Table.Inactive", $"Table {Label} is not in service.");
        if (Status == TableStatus.Cleaning)
            return Error.Conflict("Table.Cleaning", $"Table {Label} is being cleaned. Please ask a staff member.");
        return Result.Success();
    }

    /// <summary>Seats guests: creates a session and marks the table occupied.</summary>
    public Result<TableSession> OpenSession(int guests, string? waiterName, DateTimeOffset now)
    {
        var check = EnsureCanTakeOrder();
        if (check.IsFailure) return check.Error!;
        if (Status == TableStatus.Occupied || CurrentSessionId.HasValue)
            return Error.Conflict("Table.AlreadyOpen", $"Table {Label} already has an open session.");
        if (guests < 1)
            return Error.Validation("Table.Guests", "Guests must be at least 1.");

        var session = TableSession.Start(Id, guests, waiterName, now);
        CurrentSessionId = session.Id;
        Status = TableStatus.Occupied;
        Touch(now);
        Changed();
        return session;
    }

    /// <summary>Settles the table: the session must be closable (all orders finished).</summary>
    public Result CloseSession(TableSession session, bool hasUnfinishedOrders, DateTimeOffset now)
    {
        if (CurrentSessionId != session.Id)
            return Error.Conflict("Table.SessionMismatch", "This session is not the table's current session.");
        if (hasUnfinishedOrders)
            return Error.Conflict("Table.UnfinishedOrders", "Serve or cancel all orders before closing the bill.");

        var closed = session.Close(now);
        if (closed.IsFailure) return closed;

        CurrentSessionId = null;
        Status = TableStatus.Cleaning;
        Touch(now);
        Changed();
        return Result.Success();
    }

    public Result MarkClean(DateTimeOffset now)
    {
        if (Status != TableStatus.Cleaning)
            return Error.Conflict("Table.NotCleaning", $"Table {Label} is not waiting for cleaning.");
        Status = TableStatus.Free;
        Touch(now);
        Changed();
        return Result.Success();
    }

    public Result Reserve(DateTimeOffset now)
    {
        if (Status != TableStatus.Free)
            return Error.Conflict("Table.NotFree", $"Table {Label} is not free.");
        Status = TableStatus.Reserved;
        Touch(now);
        Changed();
        return Result.Success();
    }

    public void ReleaseReservation(DateTimeOffset now)
    {
        if (Status != TableStatus.Reserved) return;
        Status = TableStatus.Free;
        Touch(now);
        Changed();
    }

    /// <summary>Used when a session is closed by the stale-session job (no orders were ever placed).</summary>
    public void ForceFree(DateTimeOffset now)
    {
        CurrentSessionId = null;
        Status = TableStatus.Free;
        Touch(now);
        Changed();
    }

    private void Changed() => Raise(new TableStateChanged(Id, Status));
}
