using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MeasurementDeviceIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_measurement_monitoring_device_device_id",
                table: "measurement");

            migrationBuilder.AlterColumn<Guid>(
                name: "device_id",
                table: "measurement",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "fk_measurement_monitoring_device_device_id",
                table: "measurement",
                column: "device_id",
                principalTable: "monitoring_device",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_measurement_monitoring_device_device_id",
                table: "measurement");

            migrationBuilder.AlterColumn<Guid>(
                name: "device_id",
                table: "measurement",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_measurement_monitoring_device_device_id",
                table: "measurement",
                column: "device_id",
                principalTable: "monitoring_device",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
