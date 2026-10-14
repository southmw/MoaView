using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoaView.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "StreamingServices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    HomeUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    LoginUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    LoginUrlPattern = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    AllowedHosts = table.Column<string>(type: "TEXT", nullable: false),
                    IconPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    AccentColor = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    IsBuiltIn = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProviderCode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "generic"),
                    UserAgentOverride = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    LoginFormSpecJson = table.Column<string>(type: "TEXT", nullable: true),
                    ZoomFactor = table.Column<double>(type: "REAL", nullable: false, defaultValue: 1.0),
                    PauseOnSwitch = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    LastUrl = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamingServices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServiceId = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LoginId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    EncryptedPassword = table.Column<byte[]>(type: "BLOB", nullable: true),
                    LoginProvider = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "native"),
                    ProfileName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoFillEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    AutoSubmitEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastAutoSubmitAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LoginFailCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Accounts_StreamingServices_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "StreamingServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_ServiceId",
                table: "Accounts",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "UX_Accounts_ProfileName",
                table: "Accounts",
                column: "ProfileName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Accounts_ServiceId_Default",
                table: "Accounts",
                column: "ServiceId",
                unique: true,
                filter: "\"IsDefault\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_StreamingServices_Code",
                table: "StreamingServices",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "StreamingServices");
        }
    }
}
