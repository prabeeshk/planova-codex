using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Planova.Identity.Infrastructure.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("202610130006_ScopedRoleAssignments")]
public sealed class ScopedRoleAssignments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE identity.principal_role_assignments (
            id uuid PRIMARY KEY, organisation_id uuid NOT NULL REFERENCES identity.organisations(id),
            principal_type varchar(20) NOT NULL, principal_id uuid NOT NULL, role_id uuid NOT NULL,
            scope_type varchar(30) NOT NULL, scope_ref_id uuid NULL, valid_from timestamptz NOT NULL,
            valid_to timestamptz NULL, assigned_by uuid NOT NULL, reason varchar(500) NOT NULL,
            idempotency_key varchar(200) NOT NULL, revoked_at timestamptz NULL, revoked_by uuid NULL,
            created_at timestamptz NOT NULL, created_by uuid NOT NULL, updated_at timestamptz NOT NULL, updated_by uuid NOT NULL,
            CONSTRAINT fk_assignments_role FOREIGN KEY (organisation_id, role_id) REFERENCES identity.roles (organisation_id, id) ON DELETE RESTRICT,
            CONSTRAINT uq_assignments_org_idempotency UNIQUE (organisation_id, idempotency_key),
            CONSTRAINT ck_assignments_principal_type CHECK (principal_type IN ('user', 'group')),
            CONSTRAINT ck_assignments_dates CHECK (valid_to IS NULL OR valid_to > valid_from),
            CONSTRAINT ck_assignments_scope CHECK ((scope_type = 'Organisation' AND scope_ref_id IS NULL) OR scope_type <> 'Organisation')
        );
        CREATE INDEX ix_assignments_org_principal ON identity.principal_role_assignments
            (organisation_id, principal_type, principal_id, valid_from, valid_to);
        CREATE UNIQUE INDEX uq_assignments_active_tuple ON identity.principal_role_assignments
            (organisation_id, principal_type, principal_id, role_id, scope_type, COALESCE(scope_ref_id, '00000000-0000-0000-0000-000000000000'::uuid))
            WHERE revoked_at IS NULL AND valid_to IS NULL;
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("DROP TABLE identity.principal_role_assignments;");
}
