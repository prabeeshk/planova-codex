using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Planova.Identity.Infrastructure.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("202610130007_Delegations")]
public sealed class Delegations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE identity.delegations (
            id uuid PRIMARY KEY, organisation_id uuid NOT NULL REFERENCES identity.organisations(id),
            delegator_user_id uuid NOT NULL, delegate_user_id uuid NOT NULL, permission_filter varchar(4000) NOT NULL,
            scope_type varchar(30) NOT NULL, scope_ref_id uuid NULL, starts_at timestamptz NOT NULL, ends_at timestamptz NOT NULL,
            reason varchar(500) NOT NULL, status varchar(20) NOT NULL DEFAULT 'active', revoked_at timestamptz NULL, revoked_by uuid NULL,
            created_at timestamptz NOT NULL, created_by uuid NOT NULL, updated_at timestamptz NOT NULL, updated_by uuid NOT NULL,
            CONSTRAINT fk_delegations_delegator FOREIGN KEY (organisation_id, delegator_user_id) REFERENCES identity.users (organisation_id, id) ON DELETE RESTRICT,
            CONSTRAINT fk_delegations_delegate FOREIGN KEY (organisation_id, delegate_user_id) REFERENCES identity.users (organisation_id, id) ON DELETE RESTRICT,
            CONSTRAINT ck_delegations_users CHECK (delegator_user_id <> delegate_user_id),
            CONSTRAINT ck_delegations_dates CHECK (ends_at > starts_at),
            CONSTRAINT ck_delegations_status CHECK (status IN ('active', 'revoked', 'expired'))
        );
        CREATE INDEX ix_delegations_org_delegate ON identity.delegations (organisation_id, delegate_user_id, starts_at, ends_at);
        CREATE INDEX ix_delegations_org_delegator ON identity.delegations (organisation_id, delegator_user_id, starts_at, ends_at);
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("DROP TABLE identity.delegations;");
}
