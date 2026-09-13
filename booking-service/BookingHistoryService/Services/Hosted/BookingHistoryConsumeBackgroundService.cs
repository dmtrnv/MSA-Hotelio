using System.Globalization;
using System.Net.Mime;
using BookingHistoryService.DataAccessLayer;
using BookingHistoryService.DataAccessLayer.Entities;
using Confluent.Kafka;
using Contracts;

namespace BookingHistoryService.Services.Hosted;

public class BookingHistoryConsumeBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConsumer<string, byte[]> _consumer;
    private readonly string _bootstrapServers;
    private readonly string _topicName;
    private readonly string _consumerGroupName;

    public BookingHistoryConsumeBackgroundService(
        IServiceScopeFactory scopeFactory, 
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _bootstrapServers = configuration["Kafka:BootstrapServers"]!;
        _topicName = configuration["Kafka:Topic:Name"]!;
        _consumerGroupName = configuration["Kafka:ConsumerGroup:Name"]!;
        _consumer = new ConsumerBuilder<string, byte[]>(
                new ConsumerConfig
                {
                    BootstrapServers = _bootstrapServers,
                    GroupId = _consumerGroupName,
                    AutoOffsetReset = AutoOffsetReset.Earliest
                })
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _consumer.Subscribe(_topicName);
        while (!cancellationToken.IsCancellationRequested)
        {
            var consumedObject = _consumer.Consume(cancellationToken);
            var booking = BookingCreated.Parser.ParseFrom(consumedObject.Message.Value);
            
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.BookingHistory.AddAsync(
                new BookingHistory
                {
                    BookingId = booking.BookingId,
                    HotelId = booking.HotelId,
                    UserId = booking.UserId,
                    CreatedAt = DateTimeOffset.Parse(booking.CreatedAt)
                }, 
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
    
    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
}