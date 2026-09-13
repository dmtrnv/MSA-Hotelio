using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookingService.DataAccessLayer.Entities;

public class Booking
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }
    
    [MaxLength(255)]
    public string? UserId { get; set; }
    
    [MaxLength(255)]
    public string? HotelId { get; set; }
    
    [MaxLength(255)]
    public string? PromoCode { get; set; }
    
    public decimal? DiscountPercent { get; set; }
    
    [Column(TypeName = "timestamp(6) with time zone")]
    public DateTimeOffset CreatedAt { get; set; }
    
    [Required]
    public decimal Price { get; set; }
}