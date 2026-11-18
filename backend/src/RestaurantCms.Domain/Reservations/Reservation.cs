using System.Net.Mail;
using RestaurantCms.Domain.Common;

namespace RestaurantCms.Domain.Reservations;

public sealed class Reservation : TenantEntity
{
    public static readonly TimeSpan NoShowGrace = TimeSpan.FromMinutes(20);

    private Reservation() { }

    public string GuestName { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public int PartySize { get; private set; }
    public DateTimeOffset ReservedFor { get; private set; }
    public string? Notes { get; private set; }
    public ReservationStatus Status { get; private set; } = ReservationStatus.Requested;
    public Guid? TableId { get; private set; }
    public bool ReminderSent { get; private set; }

    public static Result<Reservation> Request(string guestName, string phone, string? email, int partySize,
        DateTimeOffset reservedFor, string? notes, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(guestName))
            return Error.Validation("Reservation.Name", "Guest name is required.");
        if (string.IsNullOrWhiteSpace(phone) || phone.Count(char.IsDigit) < 7)
            return Error.Validation("Reservation.Phone", "Enter a valid phone number.");
        if (!string.IsNullOrWhiteSpace(email) && !MailAddress.TryCreate(email.Trim(), out _))
            return Error.Validation("Reservation.Email", "Enter a valid email address.");
        if (partySize is < 1 or > 30)
            return Error.Validation("Reservation.PartySize", "Party size must be between 1 and 30.");
        if (reservedFor <= now.AddMinutes(15))
            return Error.Validation("Reservation.TooSoon", "Reservations must be at least 15 minutes ahead.");
        if (reservedFor > now.AddDays(90))
            return Error.Validation("Reservation.TooFar", "Reservations can be made up to 90 days ahead.");

        reservedFor = reservedFor.ToUniversalTime();
        return new Reservation
        {
            GuestName = guestName.Trim(),
            Phone = phone.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            PartySize = partySize,
            ReservedFor = reservedFor,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAt = now
        };
    }

    public Result Confirm(Guid? tableId, DateTimeOffset now)
    {
        if (Status is not (ReservationStatus.Requested or ReservationStatus.Confirmed))
            return Error.Conflict("Reservation.CannotConfirm", $"A {Status} reservation cannot be confirmed.");
        Status = ReservationStatus.Confirmed;
        TableId = tableId;
        Touch(now);
        Raise(new ReservationChanged(Id, Status));
        return Result.Success();
    }

    public Result Seat(DateTimeOffset now)
    {
        if (Status is not (ReservationStatus.Requested or ReservationStatus.Confirmed))
            return Error.Conflict("Reservation.CannotSeat", $"A {Status} reservation cannot be seated.");
        Status = ReservationStatus.Seated;
        Touch(now);
        Raise(new ReservationChanged(Id, Status));
        return Result.Success();
    }

    public Result Complete(DateTimeOffset now)
    {
        if (Status != ReservationStatus.Seated)
            return Error.Conflict("Reservation.CannotComplete", "Only seated reservations can be completed.");
        Status = ReservationStatus.Completed;
        Touch(now);
        Raise(new ReservationChanged(Id, Status));
        return Result.Success();
    }

    public Result Cancel(DateTimeOffset now)
    {
        if (Status is ReservationStatus.Completed or ReservationStatus.Cancelled or ReservationStatus.NoShow or ReservationStatus.Seated)
            return Error.Conflict("Reservation.CannotCancel", $"A {Status} reservation cannot be cancelled.");
        Status = ReservationStatus.Cancelled;
        Touch(now);
        Raise(new ReservationChanged(Id, Status));
        return Result.Success();
    }

    /// <summary>Marks a no-show if the guest has not been seated within the grace period. Returns true if changed.</summary>
    public bool TryMarkNoShow(DateTimeOffset now)
    {
        if (Status is not (ReservationStatus.Requested or ReservationStatus.Confirmed)) return false;
        if (now < ReservedFor + NoShowGrace) return false;
        Status = ReservationStatus.NoShow;
        Touch(now);
        Raise(new ReservationChanged(Id, Status));
        return true;
    }

    public void MarkReminderSent() => ReminderSent = true;
}
