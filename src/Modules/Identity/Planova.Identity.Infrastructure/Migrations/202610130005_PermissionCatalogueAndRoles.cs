using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Planova.Identity.Infrastructure.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("202610130005_PermissionCatalogueAndRoles")]
public sealed class PermissionCatalogueAndRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE identity.permissions (
            id uuid PRIMARY KEY, code varchar(100) NOT NULL UNIQUE, resource varchar(50) NOT NULL,
            action varchar(50) NOT NULL, description varchar(500) NOT NULL, risk_level varchar(20) NOT NULL,
            is_implemented boolean NOT NULL, is_system boolean NOT NULL DEFAULT true
        );
        CREATE TABLE identity.permission_scope_types (
            permission_id uuid NOT NULL REFERENCES identity.permissions(id) ON DELETE CASCADE,
            scope_type varchar(30) NOT NULL,
            PRIMARY KEY (permission_id, scope_type),
            CONSTRAINT ck_permission_scope_type CHECK (scope_type IN ('Self','Assigned','Team','Project','Department','Portfolio','Organisation'))
        );
        CREATE TABLE identity.roles (
            id uuid PRIMARY KEY, organisation_id uuid NOT NULL REFERENCES identity.organisations(id),
            code varchar(50) NOT NULL, name varchar(200) NOT NULL, description varchar(1000) NULL,
            is_system boolean NOT NULL DEFAULT false, status varchar(20) NOT NULL DEFAULT 'active',
            created_at timestamptz NOT NULL, created_by uuid NOT NULL, updated_at timestamptz NOT NULL, updated_by uuid NOT NULL,
            CONSTRAINT uq_roles_org_id UNIQUE (organisation_id, id),
            CONSTRAINT uq_roles_org_code UNIQUE (organisation_id, code),
            CONSTRAINT ck_roles_status CHECK (status IN ('active', 'disabled'))
        );
        CREATE TABLE identity.role_grants (
            id uuid PRIMARY KEY, organisation_id uuid NOT NULL REFERENCES identity.organisations(id),
            role_id uuid NOT NULL, permission_id uuid NOT NULL REFERENCES identity.permissions(id) ON DELETE RESTRICT,
            effect varchar(10) NOT NULL, scope_type varchar(30) NOT NULL, scope_ref_id uuid NULL,
            conditions_json jsonb NULL, created_at timestamptz NOT NULL, created_by uuid NOT NULL,
            CONSTRAINT fk_role_grants_role FOREIGN KEY (organisation_id, role_id) REFERENCES identity.roles (organisation_id, id) ON DELETE RESTRICT,
            CONSTRAINT ck_role_grants_effect CHECK (effect IN ('Allow','Deny')),
            CONSTRAINT ck_role_grants_scope CHECK ((scope_type = 'Organisation' AND scope_ref_id IS NULL) OR scope_type <> 'Organisation')
        );
        CREATE UNIQUE INDEX uq_role_grants_tuple ON identity.role_grants
            (organisation_id, role_id, permission_id, effect, scope_type, COALESCE(scope_ref_id, '00000000-0000-0000-0000-000000000000'::uuid));
        CREATE INDEX ix_role_grants_org_role ON identity.role_grants (organisation_id, role_id);

        WITH catalogue(code) AS (VALUES
            ('foundation.view'),
            ('project.view'),('project.create'),('project.edit'),('project.archive'),('project.manage_members'),('project.baseline'),('project.export'),
            ('task.view'),('task.create'),('task.edit'),('task.assign'),('task.delete'),('milestone.manage'),
            ('raid.view'),('raid.create'),('raid.edit'),('raid.assign'),('raid.escalate'),('raid.close'),
            ('change.view'),('change.create'),('change.assess'),('change.submit'),('change.cancel'),
            ('approval.view'),('approval.decide'),('approval.delegate'),('workflow.manage'),
            ('governance.view'),('governance.review'),('exception.manage'),('policy.manage'),('attestation.manage'),
            ('resource.view'),('resource.edit'),('allocation.view'),('allocation.manage'),('capacity.manage'),
            ('budget.view'),('budget.edit'),('budget.baseline'),('actual.import'),('forecast.edit'),('financial.approve'),
            ('time.enter'),('time.view_team'),('time.approve'),('leave.request'),('leave.view_team'),('leave.approve'),
            ('portfolio.view'),('portfolio.manage'),('portfolio.score'),('portfolio.snapshot'),
            ('release.view'),('release.manage'),('release.gate_decide'),
            ('report.run'),('report.design'),('audit.view'),('audit.export'),('user.manage'),('role.manage'),('settings.manage'),('integration.manage')
        )
        INSERT INTO identity.permissions(id, code, resource, action, description, risk_level, is_implemented, is_system)
        SELECT md5('planova.permission.' || code)::uuid, code, split_part(code,'.',1), split_part(code,'.',2),
            'Stable PLANOVA permission ' || code,
            CASE WHEN code IN ('role.manage','user.manage','settings.manage','audit.export','integration.manage') THEN 'high' ELSE 'standard' END,
            code IN ('foundation.view','user.manage','role.manage','settings.manage','audit.view'), true
        FROM catalogue;

        INSERT INTO identity.permission_scope_types(permission_id, scope_type)
        SELECT p.id, s.scope_type
        FROM identity.permissions p
        CROSS JOIN (VALUES ('Self'),('Assigned'),('Team'),('Project'),('Department'),('Portfolio'),('Organisation')) s(scope_type)
        WHERE (p.code = 'foundation.view' AND s.scope_type IN ('Self','Organisation'))
           OR (p.code IN ('user.manage','role.manage','settings.manage','audit.view','audit.export') AND s.scope_type IN ('Department','Organisation'))
           OR p.code NOT IN ('foundation.view','user.manage','role.manage','settings.manage','audit.view','audit.export');
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE identity.role_grants; DROP TABLE identity.roles; DROP TABLE identity.permission_scope_types; DROP TABLE identity.permissions;
        """);
}
