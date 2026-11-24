using Microsoft.Extensions.Logging;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Domain.Reservations;

namespace RestaurantCms.Infrastructure.Notifications;

/// <summary>Development sender: logs instead of sending. Replace with an SMS/WhatsApp/email provider in production.</summary>
internal sealed class LoggingNotificationSender(ILogger<LoggingNotificationSender> logger) : INotificationSender
{
    public Task SendReservationReminderAsync(Reservation reservation, string restaurantName, CancellationToken ct = default)
    {
        logger.LogInformation("Reminder → {Guest} ({Phone}): table for {Party} at {Restaurant} at {Time:u}",
            reservation.GuestName, reservation.Phone, reservation.PartySize, restaurantName, reservation.ReservedFor);
        return Task.CompletedTask;
    }
}
