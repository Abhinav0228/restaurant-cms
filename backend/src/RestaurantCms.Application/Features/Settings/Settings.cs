using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Common;
using RestaurantCms.Domain.Common;
using RestaurantCms.Domain.Settings;

namespace RestaurantCms.Application.Features.Settings;

public sealed record SettingsDto(string Name, string? Tagline, string Currency, decimal TaxPercent,
    decimal ServiceChargePercent, string TimeZoneId, string? Address, string? Phone, string? OpeningHours,
    bool QrOrderingEnabled, int DelayBufferMinutes, string? Palette = null);

public static class SettingsMapping
{
    public static SettingsDto ToDto(this RestaurantSettings s) =>
        new(s.Name, s.Tagline, s.Currency, s.TaxPercent, s.ServiceChargePercent, s.TimeZoneId, s.Address, s.Phone,
            s.OpeningHours, s.QrOrderingEnabled, s.DelayBufferMinutes, s.Palette);
}

public static class GetSettings
{
    public sealed record Query : IQuery<SettingsDto>;

    internal sealed class Handler(ISettingsRepository settings) : IQueryHandler<Query, SettingsDto>
    {
        public async Task<Result<SettingsDto>> Handle(Query query, CancellationToken ct) => (await settings.GetAsync(ct)).ToDto();
    }
}

public static class UpdateSettings
{
    public sealed record Command(SettingsDto Settings) : ICommand<SettingsDto>;

    internal sealed class Validator : Validator<Command>
    {
        public Validator()
        {
            Required("name", x => x.Settings.Name);
            MaxLength("name", x => x.Settings.Name, 100);
            Rule("currency", x => x.Settings.Currency != null && x.Settings.Currency.Trim().Length == 3, "Use a 3-letter currency code, e.g. INR.");
            Range("taxPercent", x => x.Settings.TaxPercent, 0, 50);
            Range("serviceChargePercent", x => x.Settings.ServiceChargePercent, 0, 30);
            Range("delayBufferMinutes", x => x.Settings.DelayBufferMinutes, 0, 120);
        }
    }

    internal sealed class Handler(ISettingsRepository settings, IUnitOfWork uow, IClock clock) : ICommandHandler<Command, SettingsDto>
    {
        public async Task<Result<SettingsDto>> Handle(Command command, CancellationToken ct)
        {
            var req = command.Settings;
            var s = await settings.GetAsync(ct);
            var updated = s.Update(req.Name, req.Tagline, req.Currency, req.TaxPercent, req.ServiceChargePercent,
                req.TimeZoneId ?? s.TimeZoneId, req.Address, req.Phone, req.OpeningHours, req.QrOrderingEnabled,
                req.DelayBufferMinutes, clock.UtcNow, req.Palette);
            if (updated.IsFailure) return updated.Error!;
            await uow.SaveChangesAsync(ct);
            return s.ToDto();
        }
    }
}
