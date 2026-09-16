using BookingService.Clients;
using BookingService.DataAccessLayer;
using BookingService.Services.Hosted;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Db"));
    options.UseSnakeCaseNamingConvention();
});

builder.Services.AddScoped<BookingService.Services.Application.BookingService>();

builder.Services.AddGrpcReflection();

builder.Services.AddHostedService<BookingOutboxBackgroundService>();

builder.Services.AddHttpClient<HotelioClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetSection("HotelioClient")["BaseAddress"]!);
});

var app = builder.Build();

app.MapGrpcService<BookingService.Services.Grpc.BookingGrpcService>();
app.MapGrpcReflectionService();

#region For task 4
app.MapGet("/ping", () => "pong"); 
var enableFeatureX = Environment.GetEnvironmentVariable("ENABLE_FEATURE_X") == "true";
if (enableFeatureX)
{
    app.MapGet("/feature", () => "Feature X is enabled!");
}
#endregion

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}
else
{
    app.Run();
}
