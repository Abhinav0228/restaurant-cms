using RestaurantCms.Domain.Common;

namespace RestaurantCms.Domain.Tables;

/// <summary>One visit at a table. Every order placed while the session is open is linked to it,
/// which is how a table "knows" its orders and running bill.</summary>
public sealed class TableSession : TenantEntity
{
    private TableSession() { }

    public Guid TableId { get; private set; }
    public int Guests { get; private set; }
    public string? WaiterName { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public bool IsOpen => ClosedAt is null;

    internal static TableSession Start(Guid tableId, int guests, string? waiterName, DateTimeOffset now) => new()
    {
        TableId = tableId,
        Guests = guests,
        WaiterName = string.IsNullOrWhiteSpace(waiterName) ? null : waiterName.Trim(),
        OpenedAt = now,
        CreatedAt = now
    };

    internal Result Close(DateTimeOffset now)
    {
        if (!IsOpen) return Error.Conflict("Session.Closed", "Session is already closed.");
        ClosedAt = now;
        Touch(now);
        return Result.Success();
    }

    public void UpdateGuests(int guests)
    {
        if (guests >= 1) Guests = guests;
    }

    public void AssignWaiter(string? waiterName) =>
        WaiterName = string.IsNullOrWhiteSpace(waiterName) ? null : waiterName.Trim();
}
