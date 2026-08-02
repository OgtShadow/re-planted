using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class AddTelemetryBuckets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TelemetryBuckets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BucketStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SampleCount = table.Column<int>(type: "integer", nullable: false),
                    TemperatureSum = table.Column<double>(type: "double precision", nullable: false),
                    TemperatureMin = table.Column<int>(type: "integer", nullable: false),
                    TemperatureMax = table.Column<int>(type: "integer", nullable: false),
                    HumiditySum = table.Column<double>(type: "double precision", nullable: false),
                    HumidityMin = table.Column<int>(type: "integer", nullable: false),
                    HumidityMax = table.Column<int>(type: "integer", nullable: false),
                    SoilMoistureSum = table.Column<double>(type: "double precision", nullable: false),
                    SoilMoistureMin = table.Column<int>(type: "integer", nullable: false),
                    SoilMoistureMax = table.Column<int>(type: "integer", nullable: false),
                    WaterLevelSum = table.Column<double>(type: "double precision", nullable: false),
                    WaterLevelMin = table.Column<int>(type: "integer", nullable: false),
                    WaterLevelMax = table.Column<int>(type: "integer", nullable: false),
                    LastPumpState = table.Column<bool>(type: "boolean", nullable: false),
                    LastLampState = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryBuckets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryBuckets_BucketStartUtc",
                table: "TelemetryBuckets",
                column: "BucketStartUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryBuckets_DeviceId_BucketStartUtc",
                table: "TelemetryBuckets",
                columns: new[] { "DeviceId", "BucketStartUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelemetryBuckets");
        }
    }
}
