using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Planova.Projects.Infrastructure.Migrations;

[DbContext(typeof(ProjectsDbContext))]
[Migration("202610130008_ProjectCore")]
public sealed class ProjectCore : Migration
{
    private static readonly string[] OrganisationCodeColumns = ["organisation_id", "code"];
    private static readonly string[] IdempotencyColumns = ["organisation_id", "created_by", "create_idempotency_key"];
    private static readonly string[] ScopeListColumns = ["organisation_id", "owning_organisation_unit_id", "name", "id"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("projects");
        migrationBuilder.CreateTable(name: "projects", schema: "projects", columns: table => new
        {
            id = table.Column<Guid>(nullable: false), organisation_id = table.Column<Guid>(nullable: false),
            code = table.Column<string>(maxLength: 50, nullable: false), name = table.Column<string>(maxLength: 200, nullable: false),
            description = table.Column<string>(maxLength: 4000, nullable: true), owning_organisation_unit_id = table.Column<Guid>(nullable: false),
            sponsor_user_id = table.Column<Guid>(nullable: false), manager_user_id = table.Column<Guid>(nullable: false),
            project_type = table.Column<string>(maxLength: 50, nullable: false), delivery_method = table.Column<string>(maxLength: 20, nullable: false),
            priority = table.Column<string>(maxLength: 30, nullable: false), lifecycle_status = table.Column<string>(maxLength: 20, nullable: false),
            overall_health = table.Column<string>(maxLength: 20, nullable: false), start_date = table.Column<DateOnly>(nullable: false),
            target_end_date = table.Column<DateOnly>(nullable: false), actual_end_date = table.Column<DateOnly>(nullable: true),
            progress_percentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false), currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
            create_idempotency_key = table.Column<string>(maxLength: 200, nullable: false), create_request_fingerprint = table.Column<string>(maxLength: 64, nullable: false),
            created_at = table.Column<DateTimeOffset>(nullable: false), created_by = table.Column<Guid>(nullable: false),
            updated_at = table.Column<DateTimeOffset>(nullable: false), updated_by = table.Column<Guid>(nullable: false), xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
        }, constraints: table => table.PrimaryKey("pk_projects", x => x.id));
        migrationBuilder.CreateIndex("ux_projects_organisation_code", "projects", OrganisationCodeColumns, "projects", unique: true);
        migrationBuilder.CreateIndex("ux_projects_create_idempotency", "projects", IdempotencyColumns, "projects", unique: true);
        migrationBuilder.CreateIndex("ix_projects_scope_list", "projects", ScopeListColumns, "projects");
        migrationBuilder.AddCheckConstraint("ck_projects_dates", "projects", "target_end_date >= start_date", "projects");
        migrationBuilder.AddCheckConstraint("ck_projects_progress", "projects", "progress_percentage >= 0 AND progress_percentage <= 100", "projects");
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("projects", "projects");
}
