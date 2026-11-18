using RestaurantCms.Domain.Common;

namespace RestaurantCms.Domain.Offers;

public sealed class Offer : TenantEntity
{
    private Offer() { }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }

    /// <summary>Manual on/off switch set by staff.</summary>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>Kept in sync by the offer scheduler job: enabled AND within the window.</summary>
    public bool IsLive { get; private set; }

    public static Result<Offer> Create(string title, string? description, decimal discountPercent,
        DateTimeOffset startsAt, DateTimeOffset endsAt, bool isEnabled, DateTimeOffset now)
    {
        var offer = new Offer { CreatedAt = now };
        var r = offer.Update(title, description, discountPercent, startsAt, endsAt, isEnabled, now);
        return r.IsFailure ? r.Error! : offer;
    }

    public Result Update(string title, string? description, decimal discountPercent, DateTimeOffset startsAt,
        DateTimeOffset endsAt, bool isEnabled, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Error.Validation("Offer.Title", "Offer title is required.");
        if (discountPercent is <= 0 or > 90)
            return Error.Validation("Offer.Discount", "Discount must be between 0 and 90%.");
        if (endsAt <= startsAt)
            return Error.Validation("Offer.Window", "Offer must end after it starts.");

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DiscountPercent = discountPercent;
        StartsAt = startsAt.ToUniversalTime();
        EndsAt = endsAt.ToUniversalTime();
        IsEnabled = isEnabled;
        SyncLive(now);
        Touch(now);
        return Result.Success();
    }

    /// <summary>Recomputes <see cref="IsLive"/>. Returns true when it changed.</summary>
    public bool SyncLive(DateTimeOffset now)
    {
        var live = IsEnabled && now >= StartsAt && now < EndsAt;
        if (live == IsLive) return false;
        IsLive = live;
        return true;
    }
}
