using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Planova.Identity.Infrastructure.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("202610130001_IdentityFoundation")]
public sealed class IdentityFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("identity");
        migrationBuilder.Sql("""
            CREATE TABLE identity.organisations (
                id uuid PRIMARY KEY,
                code varchar(50) NOT NULL UNIQUE,
                name varchar(200) NOT NULL,
                status varchar(20) NOT NULL,
                default_timezone varchar(100) NOT NULL,
                default_currency char(3) NOT NULL,
                created_at timestamptz NOT NULL,
                updated_at timestamptz NOT NULL
            );

            CREATE TABLE identity.users (
                id uuid PRIMARY KEY,
                organisation_id uuid NOT NULL REFERENCES identity.organisations(id),
                external_subject varchar(255) NOT NULL,
                email varchar(320) NOT NULL,
                display_name varchar(200) NOT NULL,
                status varchar(20) NOT NULL,
                last_login_at timestamptz NULL,
                created_at timestamptz NOT NULL,
                updated_at timestamptz NOT NULL,
                CONSTRAINT uq_users_org_subject UNIQUE (organisation_id, external_subject),
                CONSTRAINT uq_users_org_email UNIQUE (organisation_id, email)
            );
            CREATE INDEX ix_users_org_id ON identity.users (organisation_id, id);

            CREATE TABLE identity.user_permission_grants (
                id uuid PRIMARY KEY,
                organisation_id uuid NOT NULL REFERENCES identity.organisations(id),
                user_id uuid NOT NULL REFERENCES identity.users(id),
                permission_code varchar(100) NOT NULL,
                scope_type varchar(30) NOT NULL,
                scope_ref_id uuid NULL,
                effect varchar(10) NOT NULL,
                valid_from timestamptz NULL,
                valid_to timestamptz NULL,
                CONSTRAINT ck_grant_validity CHECK (valid_to IS NULL OR valid_from IS NULL OR valid_to > valid_from)
            );
            CREATE INDEX ix_grants_org_user_permission
                ON identity.user_permission_grants (organisation_id, user_id, permission_code);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE identity.user_permission_grants;
            DROP TABLE identity.users;
            DROP TABLE identity.organisations;
            """);
    }
}
