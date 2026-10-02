using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookingService.DataAccessLayer.Entities;

public class BookingOutbox
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    [DefaultValue(false)]
    public bool IsSent { get; set; }

    [Required]
    public long BookingId { get; set; }
    
    [ForeignKey(nameof(BookingId))]
    public Booking? Booking { get; set; }
}