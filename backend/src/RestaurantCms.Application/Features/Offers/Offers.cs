using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Common;
using RestaurantCms.Domain.Common;
using RestaurantCms.Domain.Offers;

namespace RestaurantCms.Application.Features.Offers;

public sealed record OfferDto(Guid Id, string Title, string? Description, decimal DiscountPercent,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsEnabled, bool IsLive);

public sealed record OfferFields(string Title, string? Description, decimal DiscountPercent,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsEnabled = true);

public static class OfferMapping
{
    public static OfferDto ToDto(this Offer o) =>
        new(o.Id, o.Title, o.Description, o.DiscountPercent, o.StartsAt, o.EndsAt, o.IsEnabled, o.IsLive);
}

internal sealed class OfferFieldsRules : Validator<OfferFields>
{
    public OfferFieldsRules()
    {
        Required("title", x => x.Title);
        MaxLength("title", x => x.Title, 100);
        MaxLength("description", x => x.Description, 400);
        Range("discountPercent", x => x.DiscountPercent, 1, 90);
        Rule("endsAt", x => x.EndsAt > x.StartsAt, "The offer must end after it starts.");
    }
}

public static class GetOffers
{
    public sealed record Query : IQuery<IReadOnlyList<OfferDto>>;

    internal sealed class Handler(IOfferRepository offers) : IQueryHandler<Query, IReadOnlyList<OfferDto>>
    {
        public async Task<Result<IReadOnlyList<OfferDto>>> Handle(Query query, CancellationToken ct) =>
            (await offers.GetAllAsync(ct)).OrderByDescending(o => o.StartsAt).Select(o => o.ToDto()).ToList();
    }
}

public static class CreateOffer
{
    public sealed record Command(OfferFields Fields) : ICommand<OfferDto>;

    internal sealed class Validator : Validator<Command>
    {
        public Validator() => Include(x => x.Fields, new OfferFieldsRules());
    }

    internal sealed class Handler(IOfferRepository offers, IUnitOfWork uow, IClock clock) : ICommandHandler<Command, OfferDto>
    {
        public async Task<Result<OfferDto>> Handle(Command command, CancellationToken ct)
        {
            var f = command.Fields;
            var created = Offer.Create(f.Title, f.Description, f.DiscountPercent, f.StartsAt, f.EndsAt, f.IsEnabled, clock.UtcNow);
            if (created.IsFailure) return created.Error!;
            offers.Add(created.Value);
            await uow.SaveChangesAsync(ct);
            return created.Value.ToDto();
        }
    }
}

public static class UpdateOffer
{
    public sealed record Command(Guid Id, OfferFields Fields) : ICommand<OfferDto>;

    internal sealed class Validator : Validator<Command>
    {
        public Validator() => Include(x => x.Fields, new OfferFieldsRules());
    }

    internal sealed class Handler(IOfferRepository offers, IUnitOfWork uow, IClock clock) : ICommandHandler<Command, OfferDto>
    {
        public async Task<Result<OfferDto>> Handle(Command command, CancellationToken ct)
        {
            var offer = await offers.GetAsync(command.Id, ct);
            if (offer is null) return Error.NotFound("Offer", command.Id);
            var f = command.Fields;
            var updated = offer.Update(f.Title, f.Description, f.DiscountPercent, f.StartsAt, f.EndsAt, f.IsEnabled, clock.UtcNow);
            if (updated.IsFailure) return updated.Error!;
            await uow.SaveChangesAsync(ct);
            return offer.ToDto();
        }
    }
}

public static class DeleteOffer
{
    public sealed record Command(Guid Id) : ICommand<Unit>;

    internal sealed class Handler(IOfferRepository offers, IUnitOfWork uow) : ICommandHandler<Command, Unit>
    {
        public async Task<Result<Unit>> Handle(Command command, CancellationToken ct)
        {
            var offer = await offers.GetAsync(command.Id, ct);
            if (offer is null) return Error.NotFound("Offer", command.Id);
            offers.Remove(offer);
            await uow.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}
