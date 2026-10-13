using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Planova.Identity.Infrastructure.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("202610130004_UsersAndGroups")]
public sealed class UsersAndGroups : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE identity.users DROP CONSTRAINT uq_users_org_subject;
        ALTER TABLE identity.users ADD COLUMN external_issuer varchar(500) NOT NULL DEFAULT 'legacy';
        ALTER TABLE identity.users ALTER COLUMN external_issuer DROP DEFAULT;
        ALTER TABLE identity.users ADD COLUMN job_title varchar(200) NULL;
        ALTER TABLE identity.users ADD COLUMN timezone varchar(100) NOT NULL DEFAULT 'UTC';
        ALTER TABLE identity.users ADD COLUMN locale varchar(20) NOT NULL DEFAULT 'en';
        ALTER TABLE identity.users ADD COLUMN is_emergency_administrator boolean NOT NULL DEFAULT false;
        ALTER TABLE identity.users ADD COLUMN created_by uuid NULL;
        ALTER TABLE identity.users ADD COLUMN updated_by uuid NULL;
        ALTER TABLE identity.users ADD CONSTRAINT uq_users_org_id UNIQUE (organisation_id, id);
        ALTER TABLE identity.users ADD CONSTRAINT uq_users_org_issuer_subject UNIQUE (organisation_id, external_issuer, external_subject);
        ALTER TABLE identity.users ADD CONSTRAINT ck_users_status CHECK (status IN ('active', 'suspended', 'deactivated'));

        CREATE TABLE identity.user_organisation_unit_memberships (
            id uuid PRIMARY KEY,
            organisation_id uuid NOT NULL REFERENCES identity.organisations(id),
            user_id uuid NOT NULL,
            organisation_unit_id uuid NOT NULL,
            effective_from date NOT NULL,
            effective_to date NULL,
            is_primary boolean NOT NULL DEFAULT false,
            created_at timestamptz NOT NULL,
            created_by uuid NOT NULL,
            CONSTRAINT fk_user_unit_membership_user FOREIGN KEY (organisation_id, user_id)
                REFERENCES identity.users (organisation_id, id) ON DELETE RESTRICT,
            CONSTRAINT fk_user_unit_membership_unit FOREIGN KEY (organisation_id, organisation_unit_id)
                REFERENCES identity.organisation_units (organisation_id, id) ON DELETE RESTRICT,
            CONSTRAINT uq_user_unit_membership UNIQUE (organisation_id, user_id, organisation_unit_id, effective_from),
            CONSTRAINT ck_user_unit_membership_dates CHECK (effective_to IS NULL OR effective_to >= effective_from)
        );
        CREATE UNIQUE INDEX uq_user_primary_unit_active ON identity.user_organisation_unit_memberships (organisation_id, user_id)
            WHERE is_primary AND effective_to IS NULL;

        CREATE TABLE identity.groups (
            id uuid PRIMARY KEY,
            organisation_id uuid NOT NULL REFERENCES identity.organisations(id),
            external_key varchar(255) NULL,
            name varchar(200) NOT NULL,
            type varchar(30) NOT NULL,
            status varchar(20) NOT NULL DEFAULT 'active',
            created_at timestamptz NOT NULL,
            created_by uuid NOT NULL,
            updated_at timestamptz NOT NULL,
            updated_by uuid NOT NULL,
            CONSTRAINT uq_groups_org_id UNIQUE (organisation_id, id),
            CONSTRAINT uq_groups_org_name UNIQUE (organisation_id, name),
            CONSTRAINT ck_groups_status CHECK (status IN ('active', 'disabled'))
        );
        CREATE UNIQUE INDEX uq_groups_org_external_key ON identity.groups (organisation_id, external_key) WHERE external_key IS NOT NULL;

        CREATE TABLE identity.group_members (
            id uuid PRIMARY KEY,
            organisation_id uuid NOT NULL REFERENCES identity.organisations(id),
            group_id uuid NOT NULL,
            user_id uuid NOT NULL,
            valid_from timestamptz NOT NULL,
            valid_to timestamptz NULL,
            source varchar(30) NOT NULL,
            created_at timestamptz NOT NULL,
            created_by uuid NOT NULL,
            revoked_at timestamptz NULL,
            revoked_by uuid NULL,
            CONSTRAINT fk_group_members_group FOREIGN KEY (organisation_id, group_id)
                REFERENCES identity.groups (organisation_id, id) ON DELETE RESTRICT,
            CONSTRAINT fk_group_members_user FOREIGN KEY (organisation_id, user_id)
                REFERENCES identity.users (organisation_id, id) ON DELETE RESTRICT,
            CONSTRAINT uq_group_membership_start UNIQUE (organisation_id, group_id, user_id, valid_from),
            CONSTRAINT ck_group_membership_dates CHECK (valid_to IS NULL OR valid_to > valid_from)
        );
        CREATE UNIQUE INDEX uq_group_members_active ON identity.group_members (organisation_id, group_id, user_id)
            WHERE revoked_at IS NULL AND valid_to IS NULL;
        CREATE INDEX ix_group_members_org_user ON identity.group_members (organisation_id, user_id, valid_from, valid_to);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE identity.group_members;
        DROP TABLE identity.groups;
        DROP TABLE identity.user_organisation_unit_memberships;
        ALTER TABLE identity.users DROP CONSTRAINT ck_users_status;
        ALTER TABLE identity.users DROP CONSTRAINT uq_users_org_issuer_subject;
        ALTER TABLE identity.users DROP CONSTRAINT uq_users_org_id;
        ALTER TABLE identity.users DROP COLUMN updated_by, DROP COLUMN created_by, DROP COLUMN is_emergency_administrator,
            DROP COLUMN locale, DROP COLUMN timezone, DROP COLUMN job_title, DROP COLUMN external_issuer;
        ALTER TABLE identity.users ADD CONSTRAINT uq_users_org_subject UNIQUE (organisation_id, external_subject);
        """);
}
