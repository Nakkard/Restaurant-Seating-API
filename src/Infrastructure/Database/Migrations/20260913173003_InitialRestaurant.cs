using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialRestaurant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RestaurantTables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantTables", x => x.Id);
                    table.CheckConstraint("CK_RestaurantTables_Capacity", "[Capacity] BETWEEN 2 AND 6");
                });

            migrationBuilder.CreateTable(
                name: "GuestGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArrivalOrder = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Size = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TableId = table.Column<int>(type: "int", nullable: true),
                    ArrivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SeatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LeftAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestGroups", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                    table.CheckConstraint("CK_GuestGroups_Size", "[Size] BETWEEN 1 AND 6");
                    table.CheckConstraint("CK_GuestGroups_State", "([Status] = 'Waiting' AND [TableId] IS NULL AND [SeatedAt] IS NULL AND [LeftAt] IS NULL) OR\n([Status] = 'Seated' AND [TableId] IS NOT NULL AND [SeatedAt] IS NOT NULL AND [LeftAt] IS NULL) OR\n([Status] = 'Completed' AND [TableId] IS NOT NULL AND [SeatedAt] IS NOT NULL AND [LeftAt] IS NOT NULL) OR\n([Status] = 'Left' AND [TableId] IS NULL AND [SeatedAt] IS NULL AND [LeftAt] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_GuestGroups_RestaurantTables_TableId",
                        column: x => x.TableId,
                        principalTable: "RestaurantTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "RestaurantTables",
                columns: new[] { "Id", "Capacity" },
                values: new object[,]
                {
                    { 1, 2 },
                    { 2, 3 },
                    { 3, 4 },
                    { 4, 5 },
                    { 5, 6 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuestGroups_ArrivalOrder",
                table: "GuestGroups",
                column: "ArrivalOrder",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestGroups_Status_ArrivalOrder",
                table: "GuestGroups",
                columns: new[] { "Status", "ArrivalOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestGroups_TableId_Status",
                table: "GuestGroups",
                columns: new[] { "TableId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuestGroups");

            migrationBuilder.DropTable(
                name: "RestaurantTables");
        }
    }
}
