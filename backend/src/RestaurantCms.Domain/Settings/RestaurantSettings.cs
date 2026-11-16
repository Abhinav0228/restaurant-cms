using RestaurantCms.Domain.Common;

namespace RestaurantCms.Domain.Settings;

public sealed class RestaurantSettings : TenantEntity
{
    private RestaurantSettings() { }

    public string Name { get; private set; } = "Ember & Oak";
    public string? Tagline { get; private set; }
    public string Currency { get; private set; } = "INR";
    public decimal TaxPercent { get; private set; } = 5m;
    public decimal ServiceChargePercent { get; private set; }
    public string TimeZoneId { get; private set; } = "Asia/Kolkata";
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? OpeningHours { get; private set; }
    public bool QrOrderingEnabled { get; private set; } = true;

    /// <summary>Extra minutes beyond an order's prep time before it is flagged as delayed.</summary>
    public int DelayBufferMinutes { get; private set; } = 5;

    /// <summary>Brand colour palette used by the guest site and staff portal.</summary>
    public string Palette { get; private set; } = DefaultPalette;

    public const string DefaultPalette = "navy-pink";
    public static readonly string[] Palettes = ["navy-pink", "charcoal-blue", "cream-orange"];

    public static RestaurantSettings CreateDefault() => new();

    public Result Update(string name, string? tagline, string currency, decimal taxPercent, decimal serviceChargePercent,
        string timeZoneId, string? address, string? phone, string? openingHours, bool qrOrderingEnabled,
        int delayBufferMinutes, DateTimeOffset now, string? palette = null)
    {
        if (palette is not null && !Palettes.Contains(palette))
            return Error.Validation("Settings.Palette", $"Unknown palette '{palette}'.");
        if (string.IsNullOrWhiteSpace(name))
            return Error.Validation("Settings.Name", "Restaurant name is required.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            return Error.Validation("Settings.Currency", "Use a 3-letter currency code, e.g. INR.");
        if (taxPercent is < 0 or > 50)
            return Error.Validation("Settings.Tax", "Tax must be between 0 and 50%.");
        if (serviceChargePercent is < 0 or > 30)
            return Error.Validation("Settings.Service", "Service charge must be between 0 and 30%.");
        if (delayBufferMinutes is < 0 or > 120)
            return Error.Validation("Settings.Delay", "Delay buffer must be between 0 and 120 minutes.");
        try { TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch (TimeZoneNotFoundException) { return Error.Validation("Settings.TimeZone", $"Unknown time zone '{timeZoneId}'."); }

        Name = name.Trim();
        Tagline = tagline?.Trim();
        Currency = currency.Trim().ToUpperInvariant();
        TaxPercent = taxPercent;
        ServiceChargePercent = serviceChargePercent;
        TimeZoneId = timeZoneId;
        Address = address?.Trim();
        Phone = phone?.Trim();
        OpeningHours = openingHours?.Trim();
        QrOrderingEnabled = qrOrderingEnabled;
        DelayBufferMinutes = delayBufferMinutes;
        if (palette is not null) Palette = palette;
        Touch(now);
        return Result.Success();
    }

    public TimeOnly LocalTime(DateTimeOffset utcNow) =>
        TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId)).DateTime);

    public DateOnly LocalDate(DateTimeOffset utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId)).DateTime);
}
