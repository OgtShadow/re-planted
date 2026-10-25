using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPlantSoilMoistureRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SoilMoisture_Max",
                table: "Parameters",
                type: "integer",
                nullable: false,
                defaultValue: 700);

            migrationBuilder.AddColumn<int>(
                name: "SoilMoisture_Min",
                table: "Parameters",
                type: "integer",
                nullable: false,
                defaultValue: 300);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SoilMoisture_Max",
                table: "Parameters");

            migrationBuilder.DropColumn(
                name: "SoilMoisture_Min",
                table: "Parameters");
        }
    }
}
