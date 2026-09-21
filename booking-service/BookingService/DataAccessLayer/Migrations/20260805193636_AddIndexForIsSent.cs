using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingService.DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexForIsSent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_booking_outbox_is_sent",
                table: "booking_outbox",
                column: "is_sent");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_booking_outbox_is_sent",
                table: "booking_outbox");
        }
    }
}
