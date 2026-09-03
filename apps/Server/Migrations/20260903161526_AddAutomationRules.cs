using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutomationRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PlantId = table.Column<int>(type: "integer", nullable: false),
                    SensorDeviceId = table.Column<int>(type: "integer", nullable: false),
                    SensorField = table.Column<string>(type: "text", nullable: false),
                    Condition = table.Column<string>(type: "text", nullable: false),
                    Threshold = table.Column<double>(type: "double precision", nullable: false),
                    ActuatorDeviceId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    ScheduleStartTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    ScheduleEndTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    CooldownMinutes = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    LastTriggeredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationRules_ActuatorDevices_ActuatorDeviceId",
                        column: x => x.ActuatorDeviceId,
                        principalTable: "ActuatorDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationRules_ActuatorDevices_SensorDeviceId",
                        column: x => x.SensorDeviceId,
                        principalTable: "ActuatorDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationRules_Plants_PlantId",
                        column: x => x.PlantId,
                        principalTable: "Plants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AutomationRules_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_ActuatorDeviceId",
                table: "AutomationRules",
                column: "ActuatorDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_PlantId",
                table: "AutomationRules",
                column: "PlantId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_SensorDeviceId",
                table: "AutomationRules",
                column: "SensorDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_UserId",
                table: "AutomationRules",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomationRules");
        }
    }
}
