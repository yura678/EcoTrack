using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DenormalizeEnterpriseId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "enterprise_id",
                table: "permit",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "enterprise_id",
                table: "monitoring_device",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "enterprise_id",
                table: "measurement",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "enterprise_id",
                table: "installation",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "enterprise_id",
                table: "emission_source",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "enterprise_id",
                table: "emission_limit",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "enterprise_id",
                table: "device_pollutant_capability",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "enterprise_id",
                table: "compliance_event",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "enterprise_id",
                table: "calibration_record",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Backfill EnterpriseId on tenant-owned tables by walking parent FKs.
            // Order matches dependency chain: installation -> emission_source / monitoring_device -> ...
            migrationBuilder.Sql(@"
                UPDATE installation i
                SET enterprise_id = s.enterprise_id
                FROM site s
                WHERE i.site_id = s.id AND i.enterprise_id = '00000000-0000-0000-0000-000000000000';

                UPDATE emission_source es
                SET enterprise_id = i.enterprise_id
                FROM installation i
                WHERE es.installation_id = i.id AND es.enterprise_id = '00000000-0000-0000-0000-000000000000';

                UPDATE permit p
                SET enterprise_id = i.enterprise_id
                FROM installation i
                WHERE p.installation_id = i.id AND p.enterprise_id = '00000000-0000-0000-0000-000000000000';

                UPDATE emission_limit el
                SET enterprise_id = p.enterprise_id
                FROM permit p
                WHERE el.permit_id = p.id AND el.enterprise_id = '00000000-0000-0000-0000-000000000000';

                UPDATE monitoring_device md
                SET enterprise_id = i.enterprise_id
                FROM installation i
                WHERE md.installation_id = i.id AND md.enterprise_id = '00000000-0000-0000-0000-000000000000';

                UPDATE measurement m
                SET enterprise_id = es.enterprise_id
                FROM emission_source es
                WHERE m.emission_source_id = es.id AND m.enterprise_id = '00000000-0000-0000-0000-000000000000';

                UPDATE compliance_event ce
                SET enterprise_id = es.enterprise_id
                FROM emission_source es
                WHERE ce.emission_source_id = es.id AND ce.enterprise_id = '00000000-0000-0000-0000-000000000000';

                UPDATE calibration_record cr
                SET enterprise_id = md.enterprise_id
                FROM monitoring_device md
                WHERE cr.device_id = md.id AND cr.enterprise_id = '00000000-0000-0000-0000-000000000000';

                UPDATE device_pollutant_capability dpc
                SET enterprise_id = md.enterprise_id
                FROM monitoring_device md
                WHERE dpc.device_id = md.id AND dpc.enterprise_id = '00000000-0000-0000-0000-000000000000';
            ");

            migrationBuilder.CreateIndex(
                name: "ix_permit_enterprise_id",
                table: "permit",
                column: "enterprise_id");

            migrationBuilder.CreateIndex(
                name: "ix_monitoring_device_enterprise_id",
                table: "monitoring_device",
                column: "enterprise_id");

            migrationBuilder.CreateIndex(
                name: "ix_measurement_enterprise_id",
                table: "measurement",
                column: "enterprise_id");

            migrationBuilder.CreateIndex(
                name: "ix_installation_enterprise_id",
                table: "installation",
                column: "enterprise_id");

            migrationBuilder.CreateIndex(
                name: "ix_emission_source_enterprise_id",
                table: "emission_source",
                column: "enterprise_id");

            migrationBuilder.CreateIndex(
                name: "ix_emission_limit_enterprise_id",
                table: "emission_limit",
                column: "enterprise_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_pollutant_capability_enterprise_id",
                table: "device_pollutant_capability",
                column: "enterprise_id");

            migrationBuilder.CreateIndex(
                name: "ix_compliance_event_enterprise_id",
                table: "compliance_event",
                column: "enterprise_id");

            migrationBuilder.CreateIndex(
                name: "ix_calibration_record_enterprise_id",
                table: "calibration_record",
                column: "enterprise_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_permit_enterprise_id",
                table: "permit");

            migrationBuilder.DropIndex(
                name: "ix_monitoring_device_enterprise_id",
                table: "monitoring_device");

            migrationBuilder.DropIndex(
                name: "ix_measurement_enterprise_id",
                table: "measurement");

            migrationBuilder.DropIndex(
                name: "ix_installation_enterprise_id",
                table: "installation");

            migrationBuilder.DropIndex(
                name: "ix_emission_source_enterprise_id",
                table: "emission_source");

            migrationBuilder.DropIndex(
                name: "ix_emission_limit_enterprise_id",
                table: "emission_limit");

            migrationBuilder.DropIndex(
                name: "ix_device_pollutant_capability_enterprise_id",
                table: "device_pollutant_capability");

            migrationBuilder.DropIndex(
                name: "ix_compliance_event_enterprise_id",
                table: "compliance_event");

            migrationBuilder.DropIndex(
                name: "ix_calibration_record_enterprise_id",
                table: "calibration_record");

            migrationBuilder.DropColumn(
                name: "enterprise_id",
                table: "permit");

            migrationBuilder.DropColumn(
                name: "enterprise_id",
                table: "monitoring_device");

            migrationBuilder.DropColumn(
                name: "enterprise_id",
                table: "measurement");

            migrationBuilder.DropColumn(
                name: "enterprise_id",
                table: "installation");

            migrationBuilder.DropColumn(
                name: "enterprise_id",
                table: "emission_source");

            migrationBuilder.DropColumn(
                name: "enterprise_id",
                table: "emission_limit");

            migrationBuilder.DropColumn(
                name: "enterprise_id",
                table: "device_pollutant_capability");

            migrationBuilder.DropColumn(
                name: "enterprise_id",
                table: "compliance_event");

            migrationBuilder.DropColumn(
                name: "enterprise_id",
                table: "calibration_record");
        }
    }
}
