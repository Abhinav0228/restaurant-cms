using RestaurantCms.Api.Endpoints;
using RestaurantCms.Api.Security;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Features.Offers;

namespace RestaurantCms.Api.Features.Offers;

public sealed class OfferEndpoints : IEndpointModule
{
    public void Map(ApiRoutes routes)
    {
        var group = routes.Staff.MapGroup("/offers").WithTags("Offers").RequireAuthorization(Policies.Manager);

        group.MapGet("", async (IQueryHandler<GetOffers.Query, IReadOnlyList<OfferDto>> h, CancellationToken ct) =>
            (await h.Handle(new GetOffers.Query(), ct)).ToOk());
        group.MapPost("", async (OfferFields body, ICommandHandler<CreateOffer.Command, OfferDto> h, CancellationToken ct) =>
            (await h.Handle(new CreateOffer.Command(body), ct)).ToCreated(o => $"/api/v1/offers/{o.Id}"));
        group.MapPut("/{id:guid}", async (Guid id, OfferFields body, ICommandHandler<UpdateOffer.Command, OfferDto> h, CancellationToken ct) =>
            (await h.Handle(new UpdateOffer.Command(id, body), ct)).ToOk());
        group.MapDelete("/{id:guid}", async (Guid id, ICommandHandler<DeleteOffer.Command, Unit> h, CancellationToken ct) =>
            (await h.Handle(new DeleteOffer.Command(id), ct)).ToNoContent());
    }
}
