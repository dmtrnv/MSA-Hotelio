using BookingHistoryService.DataAccessLayer;
using BookingHistoryService.Services.Hosted;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Db"));
    options.UseSnakeCaseNamingConvention();
});

builder.Services.AddHostedService<BookingHistoryConsumeBackgroundService>();

var app = builder.Build();

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
