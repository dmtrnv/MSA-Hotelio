using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookingHistoryService.DataAccessLayer.Entities;

public class BookingHistory
{
    [Key]
    public required string BookingId { get; set; }
    
    public required string HotelId { get; set; }
    
    public required string UserId { get; set; }
    
    [Column(TypeName = "timestamp(6) with time zone")]
    public DateTimeOffset CreatedAt { get; set; }
}