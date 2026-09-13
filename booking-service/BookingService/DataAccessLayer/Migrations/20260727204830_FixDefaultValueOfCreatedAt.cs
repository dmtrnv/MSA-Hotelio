using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingService.DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class FixDefaultValueOfCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "bookings",
                type: "timestamp(6) with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp(6) with time zone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "bookings",
                type: "timestamp(6) with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp(6) with time zone",
                oldDefaultValueSql: "NOW()");
        }
    }
}
