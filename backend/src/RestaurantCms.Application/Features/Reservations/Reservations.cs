using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Common;
using RestaurantCms.Application.Features.Tables;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Common;
using RestaurantCms.Domain.Reservations;

namespace RestaurantCms.Application.Features.Reservations;

public sealed record ReservationDto(Guid Id, string GuestName, string Phone, string? Email, int PartySize,
    DateTimeOffset ReservedFor, string? Notes, ReservationStatus Status, Guid? TableId, string? TableLabel, DateTimeOffset CreatedAt);

public sealed record ReservationFields(string GuestName, string Phone, string? Email, int PartySize, DateTimeOffset ReservedFor, string? Notes);

public static class ReservationMapping
{
    public static ReservationDto ToDto(this Reservation r, string? tableLabel = null) =>
        new(r.Id, r.GuestName, r.Phone, r.Email, r.PartySize, r.ReservedFor, r.Notes, r.Status, r.TableId, tableLabel, r.CreatedAt);
}

internal sealed class ReservationFieldsRules : Validator<ReservationFields>
{
    public ReservationFieldsRules()
    {
        Required("guestName", x => x.GuestName);
        MaxLength("guestName", x => x.GuestName, 80);
        Rule("phone", x => (x.Phone ?? "").Count(char.IsDigit) >= 7, "Enter a valid phone number.");
        MaxLength("email", x => x.Email, 160);
        Range("partySize", x => x.PartySize, 1, 30);
        MaxLength("notes", x => x.Notes, 500);
    }
}

public static class GetReservations
{
    public sealed record Query(DateTimeOffset? FromUtc, DateTimeOffset? ToUtc, ReservationStatus? Status) : IQuery<IReadOnlyList<ReservationDto>>;

    internal sealed class Handler(IReservationRepository reservations, ITableRepository tables) : IQueryHandler<Query, IReadOnlyList<ReservationDto>>
    {
        public async Task<Result<IReadOnlyList<ReservationDto>>> Handle(Query query, CancellationToken ct)
        {
            var list = await reservations.QueryAsync(query.FromUtc, query.ToUtc, query.Status, ct);
            var labels = (await tables.GetAllAsync(ct)).ToDictionary(t => t.Id, t => t.Label);
            return list.OrderBy(r => r.ReservedFor)
                .Select(r => r.ToDto(r.TableId is { } tid ? labels.GetValueOrDefault(tid) : null)).ToList();
        }
    }
}

/// <summary>A booking request from the public site, or a phone booking entered by staff.</summary>
public static class CreateReservation
{
    public sealed record Command(ReservationFields Fields) : ICommand<ReservationDto>;

    internal sealed class Validator : Validator<Command>
    {
        public Validator() => Include(x => x.Fields, new ReservationFieldsRules());
    }

    internal sealed class Handler(IReservationRepository reservations, IUnitOfWork uow, IJobScheduler jobs, IClock clock)
        : ICommandHandler<Command, ReservationDto>
    {
        public async Task<Result<ReservationDto>> Handle(Command command, CancellationToken ct)
        {
            var f = command.Fields;
            var created = Reservation.Request(f.GuestName, f.Phone, f.Email, f.PartySize, f.ReservedFor, f.Notes, clock.UtcNow);
            if (created.IsFailure) return created.Error!;
            reservations.Add(created.Value);
            await uow.SaveChangesAsync(ct);
            // Even unconfirmed requests are released automatically if the guest never shows.
            jobs.ScheduleNoShowCheck(created.Value.Id, created.Value.ReservedFor + Reservation.NoShowGrace);
            return created.Value.ToDto();
        }
    }
}

public static class ChangeReservationStatus
{
    public static readonly TimeSpan ReminderLead = TimeSpan.FromHours(2);

    public sealed record Command(Guid ReservationId, ReservationStatus Status, Guid? TableId) : ICommand<ReservationDto>;

    internal sealed class Handler(IReservationRepository reservations, ITableRepository tables, IUnitOfWork uow, IJobScheduler jobs, IClock clock)
        : ICommandHandler<Command, ReservationDto>
    {
        public async Task<Result<ReservationDto>> Handle(Command command, CancellationToken ct)
        {
            var reservation = await reservations.GetAsync(command.ReservationId, ct);
            if (reservation is null) return Error.NotFound("Reservation", command.ReservationId);
            var now = clock.UtcNow;

            var table = command.TableId is { } tid ? await tables.GetAsync(tid, ct) : null;
            if (command.TableId is not null && table is null) return Error.NotFound("Table", command.TableId);
            if (table is not null && table.Seats < reservation.PartySize)
                return Error.Validation("Reservation.TableTooSmall", $"{table.Label} seats {table.Seats}, the party is {reservation.PartySize}.");

            Result result;
            switch (command.Status)
            {
                case ReservationStatus.Confirmed:
                    result = reservation.Confirm(table?.Id ?? reservation.TableId, now);
                    if (result.IsSuccess)
                    {
                        var remindAt = reservation.ReservedFor - ReminderLead;
                        jobs.ScheduleReservationReminder(reservation.Id, remindAt > now ? remindAt : now.AddMinutes(1));
                        if (table is not null && reservation.ReservedFor - now <= ReminderLead && table.Status == TableStatus.Free)
                            table.Reserve(now);
                    }
                    break;

                case ReservationStatus.Seated:
                    result = reservation.Seat(now);
                    var seatTable = table ?? (reservation.TableId is { } rt ? await tables.GetAsync(rt, ct) : null);
                    if (result.IsSuccess && seatTable is not null && seatTable.CurrentSessionId is null)
                    {
                        var session = seatTable.OpenSession(reservation.PartySize, null, now);
                        if (session.IsFailure) return session.Error!;
                        tables.AddSession(session.Value);
                    }
                    break;

                case ReservationStatus.Completed:
                    result = reservation.Complete(now);
                    break;

                case ReservationStatus.Cancelled:
                    result = reservation.Cancel(now);
                    if (result.IsSuccess) await ReleaseHeldTableAsync(reservation.TableId, now, ct);
                    break;

                case ReservationStatus.NoShow:
                    result = reservation.TryMarkNoShow(now)
                        ? Result.Success()
                        : Error.Conflict("Reservation.NoShowEarly", "A no-show can only be recorded 20 minutes after the booking time.");
                    if (result.IsSuccess) await ReleaseHeldTableAsync(reservation.TableId, now, ct);
                    break;

                default:
                    result = Error.Validation("Reservation.Status", $"Cannot set status to {command.Status}.");
                    break;
            }

            if (result.IsFailure) return result.Error!;
            await uow.SaveChangesAsync(ct);
            var label = table?.Label ?? (reservation.TableId is { } t ? (await tables.GetAsync(t, ct))?.Label : null);
            return reservation.ToDto(label);
        }

        private async Task ReleaseHeldTableAsync(Guid? tableId, DateTimeOffset now, CancellationToken ct)
        {
            if (tableId is { } id && await tables.GetAsync(id, ct) is { } table) table.ReleaseReservation(now);
        }
    }
}
