using RestaurantCms.Api.Composition;

var builder = WebApplication.CreateBuilder(args);
builder.AddApi();

var app = builder.Build();
await app.UseApiAsync();
app.Run();

/// <summary>Exposed for WebApplicationFactory integration tests.</summary>
public partial class Program;
