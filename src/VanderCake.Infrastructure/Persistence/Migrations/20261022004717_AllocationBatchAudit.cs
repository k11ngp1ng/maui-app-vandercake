using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanderCake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllocationBatchAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AllocationBatchId",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AllocationBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AdministratorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllocationBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllocationBatches_AspNetUsers_AdministratorId",
                        column: x => x.AdministratorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_AllocationBatchId",
                table: "Orders",
                column: "AllocationBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationBatches_AdministratorId",
                table: "AllocationBatches",
                column: "AdministratorId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationBatches_RequestKey",
                table: "AllocationBatches",
                column: "RequestKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_AllocationBatches_AllocationBatchId",
                table: "Orders",
                column: "AllocationBatchId",
                principalTable: "AllocationBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_AllocationBatches_AllocationBatchId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "AllocationBatches");

            migrationBuilder.DropIndex(
                name: "IX_Orders_AllocationBatchId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AllocationBatchId",
                table: "Orders");
        }
    }
}
