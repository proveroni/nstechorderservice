using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderAuditing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "canceled_by",
                table: "orders",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "confirmed_by",
                table: "orders",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "orders",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "orders",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "canceled_by",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "confirmed_by",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "orders");
        }
    }
}
