using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class addModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_time",
                schema: "usr",
                table: "user-refresh-tokens");

            migrationBuilder.DropColumn(
                name: "modified_date",
                schema: "usr",
                table: "user-refresh-tokens");

            migrationBuilder.AddColumn<Guid>(
                name: "enterprise_id",
                schema: "usr",
                table: "users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "enterprise_id",
                schema: "usr",
                table: "roles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "enterprise_id",
                schema: "usr",
                table: "role-claims",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "ied_category",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ied_category", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "measure_unit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    symbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    dimension = table.Column<int>(type: "integer", nullable: false),
                    to_base_factor = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_measure_unit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pollutant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pollutant", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sector",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sector", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "enterprise",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    edrpou = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    risk_group = table.Column<int>(type: "integer", nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    sector_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enterprise", x => x.id);
                    table.ForeignKey(
                        name: "fk_enterprise_sector_sector_id",
                        column: x => x.sector_id,
                        principalTable: "sector",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "enterprise_invitation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enterprise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    token = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_used = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enterprise_invitation", x => x.id);
                    table.ForeignKey(
                        name: "fk_enterprise_invitation_enterprise_enterprise_id",
                        column: x => x.enterprise_id,
                        principalTable: "enterprise",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "site",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    sanitary_zone_radius = table.Column<int>(type: "integer", nullable: true),
                    enterprise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site", x => x.id);
                    table.ForeignKey(
                        name: "fk_site_enterprise_enterprise_id",
                        column: x => x.enterprise_id,
                        principalTable: "enterprise",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "installation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ied_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_installation", x => x.id);
                    table.ForeignKey(
                        name: "fk_installation_ied_category_ied_category_id",
                        column: x => x.ied_category_id,
                        principalTable: "ied_category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_installation_site_site_id",
                        column: x => x.site_id,
                        principalTable: "site",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "emission_source",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    installation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emission_source", x => x.id);
                    table.ForeignKey(
                        name: "fk_emission_source_installation_installation_id",
                        column: x => x.installation_id,
                        principalTable: "installation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "monitoring_plan",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    installation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_monitoring_plan", x => x.id);
                    table.ForeignKey(
                        name: "fk_monitoring_plan_installation_installation_id",
                        column: x => x.installation_id,
                        principalTable: "installation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "permit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    installation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    permit_type = table.Column<int>(type: "integer", nullable: false),
                    permit_status = table.Column<int>(type: "integer", nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    valid_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    authority = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permit", x => x.id);
                    table.ForeignKey(
                        name: "fk_permit_installation_installation_id",
                        column: x => x.installation_id,
                        principalTable: "installation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "air_emission_source",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    height = table.Column<double>(type: "double precision", nullable: false),
                    diameter = table.Column<double>(type: "double precision", nullable: false),
                    design_flow_rate = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_air_emission_source", x => x.id);
                    table.ForeignKey(
                        name: "fk_air_emission_source_emission_source_id",
                        column: x => x.id,
                        principalTable: "emission_source",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "monitoring_device",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    emission_source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    installation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    installed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_online = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_monitoring_device", x => x.id);
                    table.ForeignKey(
                        name: "fk_monitoring_device_emission_source_emission_source_id",
                        column: x => x.emission_source_id,
                        principalTable: "emission_source",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_monitoring_device_installation_installation_id",
                        column: x => x.installation_id,
                        principalTable: "installation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "water_emission_source",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    receiver = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    design_flow_rate = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_water_emission_source", x => x.id);
                    table.ForeignKey(
                        name: "fk_water_emission_source_emission_source_id",
                        column: x => x.id,
                        principalTable: "emission_source",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "monitoring_requirement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    monitoring_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emission_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pollutant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    frequency = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_monitoring_requirement", x => x.id);
                    table.ForeignKey(
                        name: "fk_monitoring_requirement_emission_source_emission_source_id",
                        column: x => x.emission_source_id,
                        principalTable: "emission_source",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_monitoring_requirement_monitoring_plan_monitoring_plan_id",
                        column: x => x.monitoring_plan_id,
                        principalTable: "monitoring_plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_monitoring_requirement_pollutant_pollutant_id",
                        column: x => x.pollutant_id,
                        principalTable: "pollutant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "emission_limit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<decimal>(type: "numeric", nullable: false),
                    period = table.Column<int>(type: "integer", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pollutant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emission_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valid_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    valid_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_emission_limit", x => x.id);
                    table.ForeignKey(
                        name: "fk_emission_limit_emission_source_emission_source_id",
                        column: x => x.emission_source_id,
                        principalTable: "emission_source",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_emission_limit_measure_unit_unit_id",
                        column: x => x.unit_id,
                        principalTable: "measure_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_emission_limit_permit_permit_id",
                        column: x => x.permit_id,
                        principalTable: "permit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_emission_limit_pollutant_pollutant_id",
                        column: x => x.pollutant_id,
                        principalTable: "pollutant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "measurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    emission_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pollutant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<decimal>(type: "numeric", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_measurement", x => x.id);
                    table.ForeignKey(
                        name: "fk_measurement_emission_source_emission_source_id",
                        column: x => x.emission_source_id,
                        principalTable: "emission_source",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_measurement_measure_unit_unit_id",
                        column: x => x.unit_id,
                        principalTable: "measure_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_measurement_monitoring_device_device_id",
                        column: x => x.device_id,
                        principalTable: "monitoring_device",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_measurement_pollutant_pollutant_id",
                        column: x => x.pollutant_id,
                        principalTable: "pollutant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exceedance_event",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    measurement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    limit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    magnitude = table.Column<decimal>(type: "numeric", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    detected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exceedance_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_exceedance_event_emission_limit_limit_id",
                        column: x => x.limit_id,
                        principalTable: "emission_limit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exceedance_event_measurement_measurement_id",
                        column: x => x.measurement_id,
                        principalTable: "measurement",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_enterprise_id",
                schema: "usr",
                table: "users",
                column: "enterprise_id");

            migrationBuilder.CreateIndex(
                name: "ix_roles_enterprise_id",
                schema: "usr",
                table: "roles",
                column: "enterprise_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_claims_enterprise_id",
                schema: "usr",
                table: "role-claims",
                column: "enterprise_id");

            migrationBuilder.CreateIndex(
                name: "ix_emission_limit_emission_source_id",
                table: "emission_limit",
                column: "emission_source_id");

            migrationBuilder.CreateIndex(
                name: "ix_emission_limit_permit_id",
                table: "emission_limit",
                column: "permit_id");

            migrationBuilder.CreateIndex(
                name: "ix_emission_limit_pollutant_id",
                table: "emission_limit",
                column: "pollutant_id");

            migrationBuilder.CreateIndex(
                name: "ix_emission_limit_unit_id",
                table: "emission_limit",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_emission_source_installation_id_code",
                table: "emission_source",
                columns: new[] { "installation_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_enterprise_edrpou",
                table: "enterprise",
                column: "edrpou",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_enterprise_sector_id",
                table: "enterprise",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_enterprise_invitation_enterprise_id",
                table: "enterprise_invitation",
                column: "enterprise_id");

            migrationBuilder.CreateIndex(
                name: "ix_exceedance_event_limit_id",
                table: "exceedance_event",
                column: "limit_id");

            migrationBuilder.CreateIndex(
                name: "ix_exceedance_event_measurement_id",
                table: "exceedance_event",
                column: "measurement_id");

            migrationBuilder.CreateIndex(
                name: "ix_ied_category_code",
                table: "ied_category",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_installation_ied_category_id",
                table: "installation",
                column: "ied_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_installation_site_id",
                table: "installation",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "ix_measure_unit_symbol",
                table: "measure_unit",
                column: "symbol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_measurement_device_id",
                table: "measurement",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_measurement_emission_source_id_pollutant_id_timestamp",
                table: "measurement",
                columns: new[] { "emission_source_id", "pollutant_id", "timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_measurement_pollutant_id",
                table: "measurement",
                column: "pollutant_id");

            migrationBuilder.CreateIndex(
                name: "ix_measurement_unit_id",
                table: "measurement",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_monitoring_device_emission_source_id",
                table: "monitoring_device",
                column: "emission_source_id");

            migrationBuilder.CreateIndex(
                name: "ix_monitoring_device_installation_id",
                table: "monitoring_device",
                column: "installation_id");

            migrationBuilder.CreateIndex(
                name: "ix_monitoring_device_serial_number",
                table: "monitoring_device",
                column: "serial_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_monitoring_plan_installation_id",
                table: "monitoring_plan",
                column: "installation_id");

            migrationBuilder.CreateIndex(
                name: "ix_monitoring_requirement_emission_source_id",
                table: "monitoring_requirement",
                column: "emission_source_id");

            migrationBuilder.CreateIndex(
                name: "ix_monitoring_requirement_monitoring_plan_id_emission_source_i",
                table: "monitoring_requirement",
                columns: new[] { "monitoring_plan_id", "emission_source_id", "pollutant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_monitoring_requirement_pollutant_id",
                table: "monitoring_requirement",
                column: "pollutant_id");

            migrationBuilder.CreateIndex(
                name: "ix_permit_installation_id",
                table: "permit",
                column: "installation_id");

            migrationBuilder.CreateIndex(
                name: "ix_permit_number",
                table: "permit",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pollutant_code",
                table: "pollutant",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pollutant_name",
                table: "pollutant",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sector_code",
                table: "sector",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_site_enterprise_id",
                table: "site",
                column: "enterprise_id");

            migrationBuilder.AddForeignKey(
                name: "fk_role_claims_enterprise_enterprise_id",
                schema: "usr",
                table: "role-claims",
                column: "enterprise_id",
                principalTable: "enterprise",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_roles_enterprise_enterprise_id",
                schema: "usr",
                table: "roles",
                column: "enterprise_id",
                principalTable: "enterprise",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_users_enterprise_enterprise_id",
                schema: "usr",
                table: "users",
                column: "enterprise_id",
                principalTable: "enterprise",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_role_claims_enterprise_enterprise_id",
                schema: "usr",
                table: "role-claims");

            migrationBuilder.DropForeignKey(
                name: "fk_roles_enterprise_enterprise_id",
                schema: "usr",
                table: "roles");

            migrationBuilder.DropForeignKey(
                name: "fk_users_enterprise_enterprise_id",
                schema: "usr",
                table: "users");

            migrationBuilder.DropTable(
                name: "air_emission_source");

            migrationBuilder.DropTable(
                name: "enterprise_invitation");

            migrationBuilder.DropTable(
                name: "exceedance_event");

            migrationBuilder.DropTable(
                name: "monitoring_requirement");

            migrationBuilder.DropTable(
                name: "water_emission_source");

            migrationBuilder.DropTable(
                name: "emission_limit");

            migrationBuilder.DropTable(
                name: "measurement");

            migrationBuilder.DropTable(
                name: "monitoring_plan");

            migrationBuilder.DropTable(
                name: "permit");

            migrationBuilder.DropTable(
                name: "measure_unit");

            migrationBuilder.DropTable(
                name: "monitoring_device");

            migrationBuilder.DropTable(
                name: "pollutant");

            migrationBuilder.DropTable(
                name: "emission_source");

            migrationBuilder.DropTable(
                name: "installation");

            migrationBuilder.DropTable(
                name: "ied_category");

            migrationBuilder.DropTable(
                name: "site");

            migrationBuilder.DropTable(
                name: "enterprise");

            migrationBuilder.DropTable(
                name: "sector");

            migrationBuilder.DropIndex(
                name: "ix_users_enterprise_id",
                schema: "usr",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_roles_enterprise_id",
                schema: "usr",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ix_role_claims_enterprise_id",
                schema: "usr",
                table: "role-claims");

            migrationBuilder.DropColumn(
                name: "enterprise_id",
                schema: "usr",
                table: "users");

            migrationBuilder.DropColumn(
                name: "enterprise_id",
                schema: "usr",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "enterprise_id",
                schema: "usr",
                table: "role-claims");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_time",
                schema: "usr",
                table: "user-refresh-tokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_date",
                schema: "usr",
                table: "user-refresh-tokens",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
