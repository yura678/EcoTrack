# EcoTrack Simulator

Standalone .NET console app that streams synthetic environmental measurement data
to a running EcoTrack API instance. Used for manual end-to-end verification of the
HMAC ingest path, TimescaleDB aggregations, materialization, and Hangfire-driven
compliance detection.

## Quick start

1. Start the API:
   ```
   dotnet run --project src/Api
   ```

2. Provision device credentials and write a local config file:
   ```
   dotnet run --project tools/Simulator -- bootstrap
   ```

   This logs in as `superAdmin@site.com`, walks two seeded enterprises, rotates
   `IngestionSecret` for two operational devices per installation, registers
   `DevicePollutantCapability` for CO, NO₂, SO₂, PM10, and writes
   `tools/Simulator/simulator.config.json`. The file contains plaintext secrets
   and is gitignored.

3. Stream measurements every 10 seconds:
   ```
   dotnet run --project tools/Simulator
   ```

   Stop with Ctrl+C.

## Subcommands and flags

```
bootstrap [--api-base URL] [--email ADDR] [--password PW]
          [--enterprises N] [--devices N] [--include-offline true]
          [--config PATH]

run       [--config PATH] [--interval SECONDS] [--once true]
```

## How it works

- **Auth:** `bootstrap` logs in via `/api/v1/auth/login/password`. For each
  enterprise it provisions devices in, it calls
  `/api/v1/auth/switch-enterprise/{id}` so subsequent calls land in the right
  tenant scope (`CompanyId` claim).
- **Secrets:** `/api/v1/monitoring-devices/{id}/rotate-ingestion-secret` is the
  only endpoint that returns the plaintext secret. Bootstrap saves it to
  `simulator.config.json` immediately. Re-running bootstrap rotates secrets
  again.
- **Capabilities:** The ingest pipeline rejects a batch if any pollutant is not
  registered as a `DevicePollutantCapability` for the device. Bootstrap calls
  `GET /api/v1/monitoring-devices/{deviceId}/capabilities` first and only POSTs
  the ones that are missing, so it's idempotent.
- **HMAC:** Every ingest POST signs `"{X-Timestamp}.{X-Nonce}.{body}"` with
  HMAC-SHA256 using the device's base64-decoded secret. The body is serialized
  once and the exact same bytes are used both for signing and for the HTTP
  request body — re-serializing or using `JsonContent.Create` breaks the
  signature.
- **Generation:** Per pollutant the simulator draws from a Gaussian around a
  diurnal-modulated mean (peak ~12:00 UTC, trough ~00:00), clamped to a safe
  range to avoid `Quality.Invalid` downgrades. 5% of values are flipped to
  `Calibration` or `Maintenance` to exercise non-Valid paths. Process
  parameters use the same Gaussian noise without diurnal modulation.

## Verifying end-to-end

- **Logs:** every 10 seconds the simulator prints
  `SN-… measurements → 202` / `SN-… process-parameters → 202`.
- **Database:**
  - `SELECT count(*), max(time) FROM raw_measurement;` grows each tick.
  - `SELECT count(*) FROM measurement_1m;` grows after each minute boundary
    (TimescaleDB continuous aggregate).
  - `SELECT * FROM "Measurement" ORDER BY "Time" DESC LIMIT 5;` shows
    materialized rows after the Hangfire 5-minute `detection-fast` job runs.
- **Hangfire dashboard:** `http://localhost:5269/hangfire` (SuperAdmin only).

## Failure-mode smoke test

Edit one device's `ingestionSecret` in the config to garbage base64, restart.
Only that device should log `401`s; the others keep flowing. This validates
per-device error isolation.

## Why a separate project?

- Decouples the simulator from `Domain`/`Application`/`Infrastructure` so it
  can target a deployed API, not just a local build.
- Lets the test client live next to the production code without affecting
  any test or CI assemblies.
