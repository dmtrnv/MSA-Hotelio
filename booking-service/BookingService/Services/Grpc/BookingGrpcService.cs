using System.Globalization;
using Booking;
using Grpc.Core;

namespace BookingService.Services.Grpc;

public class BookingGrpcService : Booking.BookingService.BookingServiceBase
{
    private readonly Application.BookingService _bookingService;

    public BookingGrpcService(Application.BookingService bookingService)
    {
        _bookingService = bookingService;
    }

    public override async Task<BookingResponse> CreateBooking(
        BookingRequest request, 
        ServerCallContext context)
    {
        var booking = await _bookingService.CreateBookingAsync(
            request.HasUserId ? request.UserId : null,
            request.HasHotelId ? request.HotelId : null,
            request.HasPromoCode? request.PromoCode : null);
        return MapToResponse(booking);
    }

    public override async Task<BookingListResponse> ListBookings(
        BookingListRequest request, 
        ServerCallContext context)
    {
        var bookings = await _bookingService.GetBookingsAsync(
            request.HasUserId ? request.UserId : null, 
            context.CancellationToken);
        return new BookingListResponse
        {
            Bookings = { bookings.Select(MapToResponse) }
        };
    }

    #region private

    private static BookingResponse MapToResponse(DataAccessLayer.Entities.Booking booking)
    {
        var response = new BookingResponse
        {
            Id = booking.Id.ToString(),
            CreatedAt = booking.CreatedAt
                .ToUniversalTime()
                .ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture),
            Price = Convert.ToDouble(booking.Price),
            DiscountPercent = Convert.ToDouble(booking.DiscountPercent)
        };
        if (booking.UserId is not null)
        {
            response.UserId = booking.UserId;
        }
        if (booking.HotelId is not null)
        {
            response.HotelId = booking.HotelId;
        }
        if (booking.PromoCode is not null)
        {
            response.PromoCode = booking.PromoCode;
        }
        return response;
    }

    #endregion
}