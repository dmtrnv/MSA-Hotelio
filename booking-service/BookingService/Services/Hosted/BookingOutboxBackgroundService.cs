using System.Globalization;
using BookingService.DataAccessLayer;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Contracts;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Services.Hosted;

public sealed class BookingOutboxBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IProducer<string, byte[]> _producer;
    private readonly string _bootstrapServers;
    private readonly string _topicName;

    public BookingOutboxBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _bootstrapServers = configuration["Kafka:BootstrapServers"]!;
        _topicName = configuration["Kafka:Topic:Name"]!;
        _producer = new ProducerBuilder<string, byte[]>(
            new ProducerConfig
            {
                BootstrapServers = _bootstrapServers,
                Acks = Acks.All
            })
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureTopicExists(_bootstrapServers);
        }
        catch
        {
        }
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var bookingsToSend = await dbContext.BookingOutbox
                    .Include(x => x.Booking)
                    .Where(x => !x.IsSent)
                    .ToListAsync(cancellationToken: cancellationToken);
                foreach (var booking in bookingsToSend)
                {
                    var bookingEvent = new BookingCreated
                    {
                        BookingId = booking.BookingId.ToString(),
                        HotelId = booking.Booking!.HotelId,
                        UserId = booking.Booking.UserId,
                        CreatedAt = booking.Booking.CreatedAt
                            .ToUniversalTime()
                            .ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture)
                    };
                    var produceResult = await _producer.ProduceAsync(
                        _topicName,
                        new Message<string, byte[]>
                        {
                            Key = bookingEvent.BookingId,
                            Value = bookingEvent.ToByteArray()
                        },
                        cancellationToken);
                    if (produceResult.Status == PersistenceStatus.Persisted)
                    {
                        booking.IsSent = true;
                        dbContext.Update(booking);
                    }
                }

                if (dbContext.ChangeTracker.HasChanges())
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            catch
            {
            }

            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
        }
    }
    
    public override void Dispose()
    {
        _producer.Dispose();
        base.Dispose();
    }
    
    #region private
    
    private async Task EnsureTopicExists(string bootstrapServers)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig
            {
                BootstrapServers = bootstrapServers
            })
            .Build();
        try
        {
            await admin.CreateTopicsAsync(
            [
                new TopicSpecification
                {
                    Name = _topicName,
                    NumPartitions = 3,
                    ReplicationFactor = 1
                }
            ]);
        }
        catch (CreateTopicsException ex)
            when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            // Ignore - topic already exists
        }
    }
    
    #endregion
}