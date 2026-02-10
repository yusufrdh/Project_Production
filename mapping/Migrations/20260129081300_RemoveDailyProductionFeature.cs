using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KP_InternalSystem.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDailyProductionFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyProductions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyProductions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DirtyCoal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Division = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Lignite = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Melawan = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Pelikan = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Pinang = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PinangHCV = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PinangHGHS = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PinangHGLS = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PinangLGHS = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PinangLGLS = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Prima = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReportDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyProductions", x => x.Id);
                });
        }
    }
}
