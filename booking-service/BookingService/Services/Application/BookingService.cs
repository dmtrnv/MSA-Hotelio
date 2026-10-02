using BookingService.Clients;
using BookingService.DataAccessLayer;
using BookingService.DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Services.Application;

public class BookingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<BookingService> _logger;
    private readonly HotelioClient _hotelioClient;

    public BookingService(
        ApplicationDbContext dbContext, 
        ILogger<BookingService> logger, 
        HotelioClient hotelioClient)
    {
        _dbContext = dbContext;
        _logger = logger;
        _hotelioClient = hotelioClient;
    }

    public async Task<IEnumerable<DataAccessLayer.Entities.Booking>> GetBookingsAsync(
        string? userId,
        CancellationToken cancellationToken = default)
    {
        return userId is null 
            ? await _dbContext.Bookings
                .ToListAsync(cancellationToken) 
            : await _dbContext.Bookings
                .Where(b => b.UserId == userId)
                .ToListAsync(cancellationToken);
    }

    public async Task<DataAccessLayer.Entities.Booking> CreateBookingAsync(
        string? userId, 
        string? hotelId, 
        string? promoCode,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "CreatingBooking: userId={UserId}, hotelId={HotelId}, promoCode={PromoCode}",
            userId, 
            hotelId, 
            promoCode);

        await ValidateUserAsync(
            userId, 
            cancellationToken);
        await ValidateHotelAsync(
            hotelId, 
            cancellationToken);

        var basePrice = await ResolveBasePriceAsync(
            userId, 
            cancellationToken);
        var discount = await ResolvePromoDiscountAsync(
            promoCode, 
            userId, 
            cancellationToken);
        var finalPrice = basePrice - discount;
        _logger.LogInformation(
            "Final price calculated: base={Base}, discount={Discount}, finalPrice={FinalPrice}",
            basePrice,
            discount,
            finalPrice);

        var booking = new DataAccessLayer.Entities.Booking
        {
            UserId = userId,
            HotelId = hotelId,
            PromoCode = promoCode,
            DiscountPercent = discount,
            Price = finalPrice
        };
        _dbContext.Bookings.Add(booking);
        var bookingOutbox = new BookingOutbox
        {
            Booking = booking
        };
        _dbContext.BookingOutbox.Add(bookingOutbox);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return booking;
    }

    private async Task ValidateUserAsync(
        string? userId,
        CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            return;
        }
        var isActiveUser = await _hotelioClient.IsActiveUserAsync(
            userId, 
            cancellationToken);
        if (!isActiveUser)
        {
            _logger.LogWarning("User {UserId} is inactive", userId);
            throw new Exception("User is inactive");
        }
        var isUserInBlackList = await _hotelioClient.IsUserInBlackListAsync(
            userId, 
            cancellationToken);
        if (isUserInBlackList)
        {
            _logger.LogWarning("User {UserId} is blacklisted", userId);
            throw new Exception("User is blacklisted");
        }
    }

    private async Task ValidateHotelAsync(
        string? hotelId,
        CancellationToken cancellationToken = default)
    {
        if (hotelId is null)
        {
            return;
        }
        var isOperationalHotel = await _hotelioClient.IsOperationalHotelAsync(
            hotelId, 
            cancellationToken);
        if (!isOperationalHotel)
        {
            _logger.LogWarning("Hotel {HotelId} is not operational", hotelId);
            throw new Exception("Hotel is not operational");
        }
        var isTrustedHotel = await _hotelioClient.IsTrustedHotelAsync(
            hotelId, 
            cancellationToken);
        if (!isTrustedHotel)
        {
            _logger.LogWarning("Hotel {HotelId} is not trusted", hotelId);
            throw new Exception("Hotel is not trusted based on reviews");
        }
        var isFullyBookedHotel = await _hotelioClient.IsFullyBookedHotelAsync(
            hotelId, 
            cancellationToken);
        if (isFullyBookedHotel)
        {
            _logger.LogWarning("Hotel {HotelId} is fully booked", hotelId);
            throw new Exception("Hotel is fully booked");
        }
    }

    private async Task<decimal> ResolveBasePriceAsync(
        string? userId,
        CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            return 100.0m;
        }
        var userStatus = await _hotelioClient.GetUserStatusAsync(
            userId, 
            cancellationToken);
        if (string.IsNullOrEmpty(userStatus))
        {
            _logger.LogDebug(
                "User {UserId} has unknown status, default base price 100.0",
                userId);
            return 100.0m;
        }
        
        var isVipUser = userStatus.Equals("VIP", StringComparison.InvariantCultureIgnoreCase);
        var basePrice = isVipUser ? 80.0m : 100.0m;
        _logger.LogDebug(
            "User {UserId} has status '{Status}', base price is {BasePrice}",
            userId,
            userStatus,
            basePrice);
        return basePrice;
    }

    private async Task<decimal> ResolvePromoDiscountAsync(
        string? promoCode, 
        string? userId,
        CancellationToken cancellationToken = default)
    {
        if (promoCode is null)
        {
            return 0.0m;
        }
        var promoDiscount = await _hotelioClient.GetPromoDiscountAsync(
            promoCode, 
            userId, 
            cancellationToken);
        if (!promoDiscount.HasValue) 
        {
            _logger.LogInformation("Promo code '{PromoCode}' is invalid or not applicable for user {UserId}", 
                promoCode, 
                userId);
            return 0.0m;
        }

        _logger.LogDebug("Promo code '{PromoCode}' applied with discount {PromoDiscount}", 
            promoCode, 
            promoDiscount);
        return promoDiscount.Value;
    }
}