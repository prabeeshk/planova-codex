using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Planova.Infrastructure.Persistence;

namespace Planova.Infrastructure.Migrations;

[DbContext(typeof(PlatformDbContext))]
[Migration("202610130002_PlatformFoundation")]
public sealed class PlatformFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("platform");
        migrationBuilder.Sql("""
            CREATE TABLE platform.audit_events (
                id uuid PRIMARY KEY,
                organisation_id uuid NOT NULL,
                actor_type varchar(30) NOT NULL,
                actor_id varchar(255) NOT NULL,
                effective_user_id uuid NULL,
                action varchar(100) NOT NULL,
                entity_type varchar(100) NOT NULL,
                entity_id uuid NULL,
                project_id uuid NULL,
                occurred_at timestamptz NOT NULL,
                outcome varchar(30) NOT NULL,
                reason varchar(500) NULL,
                correlation_id varchar(100) NOT NULL,
                causation_id varchar(100) NULL,
                source varchar(100) NOT NULL,
                changes_json jsonb NOT NULL,
                CONSTRAINT ck_audit_changes_object CHECK (jsonb_typeof(changes_json) = 'object')
            );
            CREATE INDEX ix_audit_org_time ON platform.audit_events (organisation_id, occurred_at DESC);
            CREATE INDEX ix_audit_org_entity ON platform.audit_events (organisation_id, entity_type, entity_id, occurred_at DESC);

            CREATE TABLE platform.outbox_messages (
                id uuid PRIMARY KEY,
                organisation_id uuid NOT NULL,
                event_type varchar(200) NOT NULL,
                event_version integer NOT NULL,
                aggregate_type varchar(100) NOT NULL,
                aggregate_id uuid NOT NULL,
                occurred_at timestamptz NOT NULL,
                correlation_id varchar(100) NOT NULL,
                causation_id varchar(100) NULL,
                payload_json jsonb NOT NULL,
                status varchar(30) NOT NULL,
                published_at timestamptz NULL,
                attempt_count integer NOT NULL DEFAULT 0,
                next_attempt_at timestamptz NOT NULL,
                last_error varchar(500) NULL,
                CONSTRAINT ck_outbox_payload_object CHECK (jsonb_typeof(payload_json) = 'object')
            );
            CREATE INDEX ix_outbox_pending ON platform.outbox_messages (status, next_attempt_at);
            CREATE INDEX ix_outbox_org_time ON platform.outbox_messages (organisation_id, occurred_at);

            CREATE TABLE platform.inbox_messages (
                consumer varchar(200) NOT NULL,
                message_id uuid NOT NULL,
                received_at timestamptz NOT NULL,
                processed_at timestamptz NULL,
                outcome varchar(30) NOT NULL,
                PRIMARY KEY (consumer, message_id)
            );

            CREATE OR REPLACE FUNCTION platform.prevent_audit_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'audit_events is append-only';
            END;
            $$;
            CREATE TRIGGER audit_events_append_only
                BEFORE UPDATE OR DELETE ON platform.audit_events
                FOR EACH ROW EXECUTE FUNCTION platform.prevent_audit_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER audit_events_append_only ON platform.audit_events;
            DROP FUNCTION platform.prevent_audit_mutation();
            DROP TABLE platform.inbox_messages;
            DROP TABLE platform.outbox_messages;
            DROP TABLE platform.audit_events;
            """);
    }
}
