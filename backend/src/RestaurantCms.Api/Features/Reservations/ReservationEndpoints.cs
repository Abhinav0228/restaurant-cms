using RestaurantCms.Api.Endpoints;
using RestaurantCms.Api.Security;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Features.Reservations;
using RestaurantCms.Domain;

namespace RestaurantCms.Api.Features.Reservations;

public sealed record ChangeReservationRequest(ReservationStatus Status, Guid? TableId);

public sealed class ReservationEndpoints : IEndpointModule
{
    public void Map(ApiRoutes routes)
    {
        var group = routes.Staff.MapGroup("/reservations").WithTags("Reservations").RequireAuthorization(Policies.Floor);

        group.MapGet("", async (DateTimeOffset? from, DateTimeOffset? to, ReservationStatus? status,
                IQueryHandler<GetReservations.Query, IReadOnlyList<ReservationDto>> h, CancellationToken ct) =>
            (await h.Handle(new GetReservations.Query(from, to, status), ct)).ToOk());

        group.MapPost("", async (ReservationFields body, ICommandHandler<CreateReservation.Command, ReservationDto> h, CancellationToken ct) =>
            (await h.Handle(new CreateReservation.Command(body), ct)).ToCreated(r => $"/api/v1/reservations/{r.Id}"));

        group.MapPatch("/{id:guid}/status", async (Guid id, ChangeReservationRequest body, ICommandHandler<ChangeReservationStatus.Command, ReservationDto> h, CancellationToken ct) =>
            (await h.Handle(new ChangeReservationStatus.Command(id, body.Status, body.TableId), ct)).ToOk());

        routes.Public.MapPost("/reservations", async (ReservationFields body, ICommandHandler<CreateReservation.Command, ReservationDto> h, CancellationToken ct) =>
            (await h.Handle(new CreateReservation.Command(body), ct)).ToCreated(r => $"/api/v1/reservations/{r.Id}"))
            .WithTags("Public");
    }
}
