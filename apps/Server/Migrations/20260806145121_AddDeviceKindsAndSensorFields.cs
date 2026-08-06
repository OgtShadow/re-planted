using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceKindsAndSensorFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceKind",
                table: "ActuatorDevices",
                type: "text",
                nullable: false,
                defaultValue: "actuator");

            migrationBuilder.AddColumn<string>(
                name: "ExternalDeviceId",
                table: "ActuatorDevices",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<List<string>>(
                name: "SensorFields",
                table: "ActuatorDevices",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceKind",
                table: "ActuatorDevices");

            migrationBuilder.DropColumn(
                name: "ExternalDeviceId",
                table: "ActuatorDevices");

            migrationBuilder.DropColumn(
                name: "SensorFields",
                table: "ActuatorDevices");
        }
    }
}
