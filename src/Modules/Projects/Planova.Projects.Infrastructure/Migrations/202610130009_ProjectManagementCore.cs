using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Planova.Projects.Infrastructure.Migrations;

[DbContext(typeof(ProjectsDbContext))]
[Migration("202610130009_ProjectManagementCore")]
public sealed class ProjectManagementCore : Migration
{
    private static readonly string[] StatusIndex = ["organisation_id", "project_id", "changed_at"];
    private static readonly string[] MemberIndex = ["organisation_id", "project_id", "user_id", "status"];
    private static readonly string[] HealthIndex = ["organisation_id", "project_id", "effective_date", "submitted_at"];
    private static readonly string[] BaselineIndex = ["organisation_id", "project_id", "version"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "project_status_history", schema: "projects", columns: c => new
        { id=c.Column<Guid>(nullable:false), organisation_id=c.Column<Guid>(nullable:false), project_id=c.Column<Guid>(nullable:false), from_status=c.Column<string>(maxLength:20,nullable:false), to_status=c.Column<string>(maxLength:20,nullable:false), reason=c.Column<string>(maxLength:1000,nullable:false), changed_by=c.Column<Guid>(nullable:false), changed_at=c.Column<DateTimeOffset>(nullable:false) },
        constraints: t => { t.PrimaryKey("pk_project_status_history",x=>x.id); t.ForeignKey("fk_status_project",x=>x.project_id,"projects","projects","id",onDelete:ReferentialAction.Restrict); });
        migrationBuilder.CreateTable(name: "project_members", schema: "projects", columns: c => new
        { id=c.Column<Guid>(nullable:false), organisation_id=c.Column<Guid>(nullable:false), project_id=c.Column<Guid>(nullable:false), user_id=c.Column<Guid>(nullable:false), role=c.Column<string>(maxLength:50,nullable:false), start_date=c.Column<DateOnly>(nullable:false), end_date=c.Column<DateOnly>(nullable:true), status=c.Column<string>(maxLength:20,nullable:false), created_by=c.Column<Guid>(nullable:false), created_at=c.Column<DateTimeOffset>(nullable:false), updated_by=c.Column<Guid>(nullable:false), updated_at=c.Column<DateTimeOffset>(nullable:false) },
        constraints: t => { t.PrimaryKey("pk_project_members",x=>x.id); t.ForeignKey("fk_member_project",x=>x.project_id,"projects","projects","id",onDelete:ReferentialAction.Restrict); });
        migrationBuilder.CreateTable(name: "project_health_updates", schema: "projects", columns: c => new
        { id=c.Column<Guid>(nullable:false), organisation_id=c.Column<Guid>(nullable:false), project_id=c.Column<Guid>(nullable:false), overall=c.Column<string>(maxLength:10,nullable:false), schedule=c.Column<string>(maxLength:10,nullable:false), cost=c.Column<string>(maxLength:10,nullable:false), scope=c.Column<string>(maxLength:10,nullable:false), resource=c.Column<string>(maxLength:10,nullable:false), summary=c.Column<string>(maxLength:1000,nullable:false), rationale=c.Column<string>(maxLength:2000,nullable:false), effective_date=c.Column<DateOnly>(nullable:false), corrects_update_id=c.Column<Guid>(nullable:true), submitted_by=c.Column<Guid>(nullable:false), submitted_at=c.Column<DateTimeOffset>(nullable:false) },
        constraints: t => { t.PrimaryKey("pk_project_health_updates",x=>x.id); t.ForeignKey("fk_health_project",x=>x.project_id,"projects","projects","id",onDelete:ReferentialAction.Restrict); t.ForeignKey("fk_health_correction",x=>x.corrects_update_id,"projects","project_health_updates","id",onDelete:ReferentialAction.Restrict); });
        migrationBuilder.CreateTable(name: "project_baselines", schema: "projects", columns: c => new
        { id=c.Column<Guid>(nullable:false), organisation_id=c.Column<Guid>(nullable:false), project_id=c.Column<Guid>(nullable:false), version=c.Column<int>(nullable:false), name=c.Column<string>(maxLength:200,nullable:false), scope_summary=c.Column<string>(maxLength:4000,nullable:false), start_date=c.Column<DateOnly>(nullable:false), end_date=c.Column<DateOnly>(nullable:false), planning_budget_amount=c.Column<decimal>(type:"numeric(18,2)",nullable:true), currency_code=c.Column<string>(maxLength:3,nullable:false), snapshot_hash=c.Column<string>(maxLength:64,nullable:false), status=c.Column<string>(maxLength:20,nullable:false), created_by=c.Column<Guid>(nullable:false), created_at=c.Column<DateTimeOffset>(nullable:false), established_by=c.Column<Guid>(nullable:true), established_at=c.Column<DateTimeOffset>(nullable:true) },
        constraints: t => { t.PrimaryKey("pk_project_baselines",x=>x.id); t.ForeignKey("fk_baseline_project",x=>x.project_id,"projects","projects","id",onDelete:ReferentialAction.Restrict); });
        migrationBuilder.CreateTable(name: "project_settings", schema: "projects", columns: c => new
        { project_id=c.Column<Guid>(nullable:false), organisation_id=c.Column<Guid>(nullable:false), progress_method=c.Column<string>(maxLength:20,nullable:false) },
        constraints: t => { t.PrimaryKey("pk_project_settings",x=>x.project_id); t.ForeignKey("fk_settings_project",x=>x.project_id,"projects","projects","id",onDelete:ReferentialAction.Restrict); });
        migrationBuilder.CreateIndex("ix_status_project","project_status_history",StatusIndex,"projects");
        migrationBuilder.CreateIndex("ix_members_project_user","project_members",MemberIndex,"projects");
        migrationBuilder.CreateIndex("ix_health_project_effective","project_health_updates",HealthIndex,"projects");
        migrationBuilder.CreateIndex("ix_health_correction","project_health_updates","corrects_update_id","projects");
        migrationBuilder.CreateIndex("ux_baselines_project_version","project_baselines",BaselineIndex,"projects",unique:true);
        migrationBuilder.Sql("INSERT INTO projects.project_settings (project_id, organisation_id, progress_method) SELECT id, organisation_id, 'manual' FROM projects.projects ON CONFLICT DO NOTHING");
        migrationBuilder.AddCheckConstraint("ck_members_dates","project_members","end_date IS NULL OR end_date >= start_date","projects");
        migrationBuilder.AddCheckConstraint("ck_baseline_dates","project_baselines","end_date >= start_date","projects");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("project_settings","projects"); migrationBuilder.DropTable("project_baselines","projects");
        migrationBuilder.DropTable("project_health_updates","projects"); migrationBuilder.DropTable("project_members","projects");
        migrationBuilder.DropTable("project_status_history","projects");
    }
}
