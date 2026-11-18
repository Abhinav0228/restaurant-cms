namespace RestaurantCms.Domain;

public enum TableStatus
{
    Free,
    Occupied,
    Reserved,
    Cleaning
}

public enum OrderType
{
    DineIn,
    Takeaway,
    QrDineIn
}

public enum OrderStatus
{
    Pending,
    Accepted,
    Preparing,
    Ready,
    Served,
    Completed,
    Cancelled
}

public enum ReservationStatus
{
    Requested,
    Confirmed,
    Seated,
    Completed,
    Cancelled,
    NoShow
}

public enum StaffRole
{
    /// <summary>Runs the SaaS platform: onboards restaurants. Not tied to a tenant.</summary>
    PlatformAdmin,
    /// <summary>Restaurant owner / admin.</summary>
    Admin,
    Manager,
    Waiter,
    Kitchen
}

public enum DietaryTag
{
    Vegetarian,
    Vegan,
    GlutenFree,
    Spicy,
    ChefsPick,
    New,
    ContainsNuts
}
