using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class AddActuatorDevicesManyToManyGoMapped : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActuatorDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    GoCommand = table.Column<string>(type: "text", nullable: false),
                    GoCommandPath = table.Column<string>(type: "text", nullable: false),
                    GoStateField = table.Column<string>(type: "text", nullable: false),
                    TargetParameter = table.Column<string>(type: "text", nullable: false),
                    EffectType = table.Column<string>(type: "text", nullable: false),
                    EffectStrength = table.Column<double>(type: "double precision", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActuatorDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActuatorDevices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlantActuatorDevices",
                columns: table => new
                {
                    PlantId = table.Column<int>(type: "integer", nullable: false),
                    ActuatorDeviceId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantActuatorDevices", x => new { x.PlantId, x.ActuatorDeviceId });
                    table.ForeignKey(
                        name: "FK_PlantActuatorDevices_ActuatorDevices_ActuatorDeviceId",
                        column: x => x.ActuatorDeviceId,
                        principalTable: "ActuatorDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlantActuatorDevices_Plants_PlantId",
                        column: x => x.PlantId,
                        principalTable: "Plants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActuatorDevices_UserId",
                table: "ActuatorDevices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlantActuatorDevices_ActuatorDeviceId",
                table: "PlantActuatorDevices",
                column: "ActuatorDeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlantActuatorDevices");

            migrationBuilder.DropTable(
                name: "ActuatorDevices");
        }
    }
}
