using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Planova.Identity.Infrastructure.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("202610130003_OrganisationHierarchy")]
public sealed class OrganisationHierarchy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE identity.organisations ADD COLUMN created_by uuid NULL;
        ALTER TABLE identity.organisations ADD COLUMN updated_by uuid NULL;

        CREATE TABLE identity.organisation_units (
            id uuid PRIMARY KEY,
            organisation_id uuid NOT NULL REFERENCES identity.organisations(id),
            parent_id uuid NULL,
            type varchar(50) NOT NULL,
            code varchar(50) NOT NULL,
            name varchar(200) NOT NULL,
            path varchar(2000) NOT NULL,
            status varchar(20) NOT NULL DEFAULT 'active',
            created_at timestamptz NOT NULL,
            created_by uuid NOT NULL,
            updated_at timestamptz NOT NULL,
            updated_by uuid NOT NULL,
            CONSTRAINT uq_organisation_units_org_id UNIQUE (organisation_id, id),
            CONSTRAINT uq_organisation_units_org_code UNIQUE (organisation_id, code),
            CONSTRAINT fk_organisation_units_parent FOREIGN KEY (organisation_id, parent_id)
                REFERENCES identity.organisation_units (organisation_id, id) ON DELETE RESTRICT,
            CONSTRAINT ck_organisation_units_parent CHECK (parent_id IS NULL OR parent_id <> id),
            CONSTRAINT ck_organisation_units_status CHECK (status IN ('active', 'inactive'))
        );
        CREATE INDEX ix_organisation_units_org_path ON identity.organisation_units (organisation_id, path, id);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE identity.organisation_units;
        ALTER TABLE identity.organisations DROP COLUMN updated_by;
        ALTER TABLE identity.organisations DROP COLUMN created_by;
        """);
}
