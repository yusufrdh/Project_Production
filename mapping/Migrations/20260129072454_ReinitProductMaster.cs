using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KP_InternalSystem.Migrations
{
    /// <inheritdoc />
    public partial class ReinitProductMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- 1. HAPUS Constraint & Kolom created_at (Script Safety) ---
            migrationBuilder.Sql(
                @"DECLARE @ConstraintName nvarchar(200)
                SELECT @ConstraintName = Name FROM SYS.DEFAULT_CONSTRAINTS
                WHERE PARENT_OBJECT_ID = OBJECT_ID('dbo.product')
                AND PARENT_COLUMN_ID = (SELECT column_id FROM sys.columns WHERE NAME = 'created_at' AND object_id = OBJECT_ID('dbo.product'))
                
                IF @ConstraintName IS NOT NULL
                EXEC('ALTER TABLE dbo.product DROP CONSTRAINT ' + @ConstraintName)"
            );
            migrationBuilder.Sql("IF COL_LENGTH('dbo.product', 'created_at') IS NOT NULL ALTER TABLE dbo.product DROP COLUMN created_at");

            // --- 2. TAMBAH KOLOM id_division (VERSI AMAN) ---
            
            // A. Tambah sebagai NULLABLE dulu (Biar gak error pas dibuat)
            migrationBuilder.AddColumn<int>(
                name: "id_division",
                table: "product",
                type: "int",
                nullable: true); // Boleh kosong dulu

            // B. UPDATE DATA: Isi id_division dengan ID Divisi pertama yang ditemukan di tabel 'division'
            // (Ini mencegah error FK karena datanya jadi valid)
            migrationBuilder.Sql("UPDATE product SET id_division = (SELECT TOP 1 id_division FROM division)");
            
            // C. Kalau ternyata tabel division kosong melompong, kita kasih default 1 (Harapan ada ID 1)
            // (Optional safety net)
            migrationBuilder.Sql("UPDATE product SET id_division = 1 WHERE id_division IS NULL");

            // D. Sekarang datanya sudah terisi, kita ubah jadi WAJIB ISI (NOT NULL)
            migrationBuilder.AlterColumn<int>(
                name: "id_division",
                table: "product",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // --- 3. LANJUTKAN SISANYA (Product Alias & Index) ---

            // Tambah kolom is_active
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "product",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Buat tabel product_alias
            migrationBuilder.CreateTable(
                name: "product_alias",
                columns: table => new
                {
                    id_alias = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    alias_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    id_product = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_alias", x => x.id_alias);
                    table.ForeignKey(
                        name: "FK_product_alias_product_id_product",
                        column: x => x.id_product,
                        principalTable: "product",
                        principalColumn: "id_product",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create Index
            migrationBuilder.CreateIndex(
                name: "IX_product_id_division",
                table: "product",
                column: "id_division");

            migrationBuilder.CreateIndex(
                name: "IX_product_alias_id_product",
                table: "product_alias",
                column: "id_product");

            // Add Foreign Key (Sekarang pasti aman karena id_division sudah diisi data valid)
            migrationBuilder.AddForeignKey(
                name: "FK_product_division_id_division",
                table: "product",
                column: "id_division",
                principalTable: "division",
                principalColumn: "id_division",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyProductions");

            migrationBuilder.DropTable(
                name: "pit_alias",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "product_alias");

            migrationBuilder.DropTable(
                name: "UserActivityLog");

            migrationBuilder.DropTable(
                name: "pit_official");

            migrationBuilder.DropTable(
                name: "product");

            migrationBuilder.DropTable(
                name: "department",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "location",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "division",
                schema: "dbo");
        }
    }
}
