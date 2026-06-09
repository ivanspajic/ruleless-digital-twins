# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to
follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This is a research/demo deliverable; versions track the product line rather than
a published package.

## [Unreleased]

_Nothing yet._

## [0.1.0] - 2026-06-02

First consolidated product release of the Ruleless Digital Twins SmartNode for
Home Assistant. The repository was refocused from a working sandbox into a
professional deliverable centred on the .NET SmartNode, Home Assistant
integration, the MAPE-K loop, safety gates, and an offline demo.

### Added

- **Health and readiness endpoints.** `GET /api/health` (liveness, always `200`)
  and `GET /api/ready` (returns `503` until the configured local stores are
  reachable). No Home Assistant token required.
- **Read-only product dashboard** for observing decisions and state.
- **Minimal offline Docker packaging.** Root `docker-compose.yml`, lean
  `Dockerfile.offline`, and `.env.docker.example` run SmartNode on port `8080`
  with replay prices and SQLite-backed state. The container healthcheck uses
  `/api/health`.
- **Home Assistant live setup hardening.** Documented and verified the read-only
  `/api/ha/connection`, `/api/ha/discovery`, and `/api/model/validation?live=true`
  endpoints, with a unit-tested fail-closed live validator.
- **Persisted safety settings.** Non-secret settings store keys
  (`mapek.allow_execution`, `mapek.kill_switch`, `mapek.allowed_entities`,
  `mapek.allowed_services`, `mapek.max_actions_per_hour`, `mapek.cooldown_seconds`,
  `mapek.autonomous_execution_enabled`) merge with environment variables through a
  fail-closed resolver: invalid values are ignored with a warning, limits can only
  tighten, and `TOKEN_HA` is never read from the store.
- **Offline CI gate** enforcing the .NET safety/persistence suites and the
  `hass-to-rdt` Python tests on every push.

### Changed

- Repository refocused as a product deliverable; internal AI-workflow and
  meta-process documents removed from the active tree (historical material kept
  under `docs/archive/`).

### Security

- Real Home Assistant actuation stays fail-closed: it requires
  `mapek.allow_execution`, a non-dry-run request, a present `TOKEN_HA`, the kill
  switch off, and the action's entity/service on the allowlists. The autonomous
  loop remains dry-run.
- No secrets are committed; `.env.example` and `.env.docker.example` contain
  placeholders only.
