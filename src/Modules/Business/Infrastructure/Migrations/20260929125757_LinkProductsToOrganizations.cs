using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiGrowthPlatform.Business.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LinkProductsToOrganizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_products_OrganizationId",
                schema: "business",
                table: "products",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_products_organizations_OrganizationId",
                schema: "business",
                table: "products",
                column: "OrganizationId",
                principalSchema: "business",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_organizations_OrganizationId",
                schema: "business",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_OrganizationId",
                schema: "business",
                table: "products");
        }
    }
}
