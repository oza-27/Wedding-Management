using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingSaaS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueUserEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "email_index",
                schema: "public",
                table: "asp_net_users");

            migrationBuilder.CreateIndex(
                name: "ux_asp_net_users_normalized_email",
                schema: "public",
                table: "asp_net_users",
                column: "normalized_email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_asp_net_users_normalized_email",
                schema: "public",
                table: "asp_net_users");

            migrationBuilder.CreateIndex(
                name: "email_index",
                schema: "public",
                table: "asp_net_users",
                column: "normalized_email");
        }
    }
}
