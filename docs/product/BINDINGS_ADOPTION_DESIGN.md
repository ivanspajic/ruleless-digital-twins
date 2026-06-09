# Safe bindings adoption — design (design-only, no code)

> **Status:** design draft. No endpoint, wizard, dashboard, MAPE-K, or
> Home Assistant execution change ships with this document. It only decides
> *how* a validated binding config should later be saved/adopted safely.

Closes the design gap after:

- **#152** — wizard now does `discover → generate → export → edit JSON → validate inline`
  (reuses `POST /api/ha/bindings/validate`; validation only, no save).
- **#153** — CI `js-syntax` job parse-checks `setup.js` / `discovery.js` / `dashboard.js`.

Next real product block: **`validated bindings → save/adopt safely`** — the first
feature that can write the product's real runtime config. This doc designs it
before any code.

---

## 0. Decisions (locked 2026-06-03)

These are decided, not open. The rest of the doc is consistent with them.

1. **Adopted runtime file:** `state-data/ha-bindings.runtime.json` (gitignored).
   **Never** write into the tracked `config/ha-bindings.<profile>.json` files.
   The adopted file is consumed via `HA_BINDINGS_FILE`.
2. **Reload:** first slice is **restart-required**; **no hot-reload**. API returns
   `reloadRequired: true`.
3. **Audit / revisions:** a **dedicated `IBindingsRevisionStore`** (revisions +
   backups + adoption metadata) **plus** a high-level adoption **event in
   `ISafetyEventLog`**. No secrets stored.
4. **Validation rules:** `PASS` → adoptable; `WARN` → adoptable **only** with
   explicit `acceptWarnings: true`; `FAIL` → always rejected (`422`); invalid
   JSON → rejected (`400`).
5. **Stale-write protection:** the request carries `expectedCurrentHash`. If it
   differs from the live runtime file's hash → `409`. The response returns
   `currentHash`, `adoptedHash`, and `revisionId`.
6. **Safety gates:** `dryRun: true` supported (no write); real write gated by
   `HA_BINDINGS_ADOPTION_ENABLED=true` (off → `403`, fail-closed); backup
   mandatory before overwrite; atomic write where possible; rollback is a
   separate endpoint/PR.
7. **Multi-profile:** first slice adopts the **currently active runtime file**;
   `profile` is **metadata only**; named multi-profile adoption is future work.

---

## 1. Audit (read-only findings)

Grounded in the current `main`.

### 1.1 Source of truth for bindings

- Bindings are loaded **once at startup** in
  [`Factory.cs`](../../SmartNode/SmartNode/Factory.cs) (~L246–267):
  read `HA_BINDINGS_FILE`; if unset, resolve the bundled
  `config/ha-bindings.showcase.json` via
  `HaBindingsLoader.ResolveDefaultShowcasePath()`.
- `HaBindingsLoader.Load(path)`
  ([`HaBindings/HaBindingsLoader.cs`](../../SmartNode/SmartNode/HaBindings/HaBindingsLoader.cs))
  reads the file, deserializes to `HaBindingsConfig`, builds sensor/actuator maps.
- **The file is the single source of truth.** There is **no hot-reload** for
  bindings: `Factory` builds the maps at construction. (Goals *are* hot-reloaded
  each tick — bindings are not; see `Program.cs:395`.)
  → **Adopting new bindings requires a process restart (or an explicit reload).**

### 1.2 Validation (already exists, reuse it)

- `BindingsValidator.ValidateConfig(cfg, label)` →
  `ValidationReport { Status (PASS|WARN|FAIL), HasFailures, ErrorCount,
  WarningCount, Profile, SensorCount, ActuatorCount, Issues[] }`.
- `POST /api/ha/bindings/validate`
  ([`Validation/BindingsConfigValidationEndpoint.cs`](../../SmartNode/SmartNode/Validation/BindingsConfigValidationEndpoint.cs)):
  `400` on empty/unparseable body; `200` with the report otherwise. Same parse
  options as the loader (case-insensitive, trailing commas, `//` comments).
- `--validate-model` CLI and `/api/model/validation` validate `HA_BINDINGS_FILE`
  with **no client-controlled path** (`Program.cs:1421`).

### 1.3 Discovery / export (produces the candidate config)

- `GET /api/ha/discovery` → groups/entities;
  `POST /api/ha/discovery/draft` → review-only draft;
  `POST /api/ha/discovery/draft/export` → `{ config, counts, validation, warnings }`.
- **The server never writes the config to disk today.** Export returns JSON; the
  browser copies/downloads it. The wizard then validates edited JSON in-memory.

### 1.4 Persistence layer (reusable for revisions/audit)

Established pattern under `SmartNode/SmartNode/Services/<area>/`:
`I<X>` interface + `Sqlite<X>` + `InMemory<X>`, provider selected by env var.

| Area | Interface | SQLite impl |
|---|---|---|
| Settings | `ISettingsStore` | `Services/Settings/SqliteSettingsStore.cs` |
| Safety **audit log** | `ISafetyEventLog` | `Services/Safety/SqliteSafetyEventLog.cs` |
| Goals | — | `Services/Goals/SqliteGoalRepository.cs` |
| Execution history | `IExecutionHistory` | `Services/Execution/SqliteExecutionHistory.cs` |
| Decisions | — | `Services/Decisions/SqliteDecisionLog.cs` |

→ A `IBindingsRevisionStore` + `SqliteBindingsRevisionStore` + in-memory fake
would follow the exact same template, and adoption events can be recorded
through the existing audit-log seam.

### 1.5 Git / no-secret rules (`.gitignore`)

- `config/ha-bindings.<profile>.json` (showcase, testlab) are **tracked** in the repo.
- `config/ha-bindings.*.draft.json`, `*.sqlite`/`*.db`, `state-data/`, `.env*`
  (except examples), `secrets.json` are **ignored**.
- **Implication:** writing adopted bindings *into a tracked profile file* would
  dirty git and risk an accidental commit. The adopted runtime file must live in
  a **gitignored runtime path** (e.g. `state-data/`), with `HA_BINDINGS_FILE`
  pointing at it.

### 1.6 No-secret posture

`TOKEN_HA` is never persisted and never returned. Bindings JSON contains entity
IDs / kinds only — **no token**. Adoption must keep it that way: reject any body
that looks like it carries a secret, and never log the full token.

### 1.7 Offline tests

Validate / discovery / exporter paths are covered and run in the offline CI
filter. Any adoption code must stay **offline-testable** (temp dirs, fakes, no
live HA, no real filesystem outside a temp path).

---

## 2. Design options

### Option A — File-only adoption
Write the adopted config to the `HA_BINDINGS_FILE` path; timestamped backup before overwrite.

- **Pros:** smallest change; matches the current "file is source of truth"; works with the existing loader unchanged; trivially offline-testable.
- **Cons:** no revision history beyond loose backup files; no structured audit; reload still needs a restart; if `HA_BINDINGS_FILE` points at a *tracked* profile, it dirties git.
- **Risks:** clobbering a healthy config; backups pile up untracked; concurrent writes.
- **Tests:** validate-before-write; refuse FAIL; backup created; atomic write; secret rejected.
- **Rollback:** restore the latest backup file (manual or a small endpoint).
- **UX:** "Adopt" writes file → "restart required".
- **Complexity:** low.

### Option B — SQLite as source of truth
Store adopted bindings in the DB; the runtime reads bindings from the DB instead of a file.

- **Pros:** clean product model; revisions + audit native; rollback = activate a prior revision.
- **Cons:** large change — `Factory`/loader must read from DB; diverges from the file-based loader; bigger blast radius; the showcase/testlab/offline demo all assume a file.
- **Risks:** regressing the offline/demo path; migration of the existing file profiles.
- **Tests:** DB round-trip, runtime-reads-DB, revision activation, restart survival — substantial.
- **Rollback:** activate previous revision row.
- **UX:** same wizard, but no file to ship around.
- **Complexity:** high.

### Option C — Hybrid (recommended)
DB keeps **revisions + audit**; the **file stays the runtime input** the existing loader consumes. Adoption = validate → backup current file → atomic write to a runtime file (gitignored) → record a revision + audit event in SQLite.

- **Pros:** keeps the proven file loader unchanged (low risk to demos/offline); adds structured history/audit/rollback without rewriting `Factory`; fits the existing `Services/*` SQLite pattern; the adopted file lives in a gitignored runtime path so git stays clean.
- **Cons:** two stores to keep coherent (file + DB row); reload still needs a restart in the first slice.
- **Risks:** file/DB drift if a write half-completes → mitigate with "write file first, then record revision; on revision-write failure, keep file + log a warning" (file is the runtime truth).
- **Tests:** validate-gate, backup, atomic write, revision recorded, audit event, secret rejected, FAIL refused, WARN-needs-opt-in, offline.
- **Rollback:** revision store lists prior versions; a later rollback endpoint restores a chosen revision's file + records the rollback.
- **UX:** wizard "Adopt validated bindings" → preview → confirm → result (backup path, revision id, `reloadRequired: true`).
- **Complexity:** medium.

**Recommendation: Option C.** It is the safest near-term compromise: no change to
how bindings are *consumed* (file + existing loader, so the offline demo is
untouched), while gaining backup, structured revisions, audit, and a clean
rollback path using machinery that already exists.

---

## 3. Proposed API contract (not implemented)

### `POST /api/ha/bindings/adopt`

Request:
```json
{
  "profile": "showcase",
  "config": { "...": "full HaBindingsConfig JSON" },
  "expectedValidationStatus": "PASS",
  "acceptWarnings": false,
  "expectedCurrentHash": "sha256:<hash of the runtime config the edit was based on>",
  "reason": "Adopt edited bindings from setup wizard",
  "dryRun": false
}
```

Response (success):
```json
{
  "adopted": true,
  "dryRun": false,
  "backupPath": "state-data/bindings-backups/ha-bindings.runtime.2026-06-03T01-12-00Z.json",
  "revisionId": "rev_000007",
  "currentHash": "sha256:<hash of the file before this adoption>",
  "adoptedHash": "sha256:<hash of the newly written config>",
  "validation": { "status": "PASS", "errorCount": 0, "warningCount": 0, "issues": [] },
  "reloadRequired": true,
  "warnings": []
}
```

`dryRun: true` returns the same shape with `adopted: false` and **no write**
(adoption preview), so the wizard can show exactly what would happen.

Error codes:

| Code | When |
|---|---|
| `400` | body empty / unparseable JSON / secret-like content detected |
| `403` | adoption writes disabled (env flag off) |
| `409` | current runtime config changed since the client read it (stale write) |
| `422` | validation `FAIL`, or `WARN` without `acceptWarnings:true` |
| `500` | filesystem / backup error |

The token is never accepted in `config`, never echoed, never logged.

---

## 4. Safety model

1. **Validate before write** — re-run `BindingsValidator.ValidateConfig` server-side; never trust the client's status.
2. **Refuse FAIL** (`422`). **WARN** only with explicit `acceptWarnings: true`.
3. **Mandatory backup** of the current runtime file before overwrite (timestamped, in a gitignored `state-data/` path).
4. **Atomic write** — write to a temp file then move/replace, so a crash never leaves a half-written config.
5. **No secret** — reject any `TOKEN_HA`/bearer-looking content; bindings carry entity IDs only.
6. **Audit log mandatory** — record adoption (who/when/reason/profile/revision/validation summary) via the existing audit-log seam; secrets-free.
7. **Write opt-in** — `HA_BINDINGS_ADOPTION_ENABLED=true` gates real writes; off → `403`, fail-closed (mirrors `MAPEK_ALLOW_EXECUTION`).
8. **Dry-run / preview** — `dryRun:true` computes validation + backup target + revision id + hashes without writing.
9. **Stale-write guard (mandatory)** — the request carries `expectedCurrentHash` (hash of the runtime config the edit was based on). If it differs from the live file's hash → `409`. The response returns `currentHash` + `adoptedHash` so the client can re-sync.
10. **Rollback designed in** — every adoption is reversible from the revision store.
11. **Runtime path, not a tracked profile** — adopted file lives in a gitignored runtime location; tracked `config/ha-bindings.<profile>.json` demo files stay read-only references.
12. **Reload honesty** — response says `reloadRequired: true` until/unless hot-reload is added.

---

## 5. Proposed UX (wizard) — described, not built

1. Export runtime config (existing).
2. Editor seeded with the exported config (existing, #152).
3. User edits the JSON (existing).
4. **Validate** (existing) → PASS/WARN/FAIL.
5. **If PASS** (or WARN + explicit accept): reveal an **"Adopt validated bindings"** button.
6. Click → **adoption preview** (`dryRun:true`): show validation summary, backup target, revision id, and whether a restart/reload is required. Nothing written yet.
7. **Confirm** → real adopt (`dryRun:false`).
8. Result: backup path + revision id + clear **"restart required to apply"** notice.
9. Failure states reuse the existing typed-error rendering (FAIL → 422, write disabled → 403, etc.).

> The adopt button is **not** coded in the design PR. This section only defines
> the target behaviour.

---

## 6. Implementation plan (small PRs)

| PR | Scope | Likely files | Key tests | Done when |
|---|---|---|---|---|
| **PR 1** | Backend design seam + test skeletons (no behaviour) | `Services/Bindings/IBindingsAdoptionService.cs`, fakes; test stubs | skeleton compiles; offline filter unchanged | interfaces + failing/red skeletons exist |
| **PR 2** | Adoption endpoint **dry-run only** | `Services/Bindings/BindingsAdoptionEndpoint.cs`, `Program.cs` route, tests | dry-run returns validation + planned backup/revision; **never writes**; FAIL→422; secret→400 | `POST /api/ha/bindings/adopt` with `dryRun:true` works, no writes |
| **PR 3** | File backup + atomic write (real adopt behind env flag) | adoption service write path, env flag | backup created; atomic temp-then-move; flag off→403; refuse FAIL/WARN | real write works, gated, with backup |
| **PR 4** | Audit + SQLite revision tracking | `Services/Bindings/SqliteBindingsRevisionStore.cs` (+ interface, in-memory), audit-log call | revision row written; audit event recorded; round-trip + restart-survival | adoption is traceable and listable |
| **PR 5** | Wizard **Adopt** button behind validation PASS | `setup.html`, `discovery.js` (+ CI js-syntax covers it) | preview→confirm flow; PASS-gated; reload notice | UX flow live end-to-end |
| **PR 6** | Rollback endpoint or CLI | adoption service rollback path, `Program.cs`/CLI, tests | restore a chosen revision; backup of pre-rollback state; audit event | a prior revision can be restored safely |
| **PR 7** | Docs + demo evidence | README, ROADMAP/AC, demo script | doc link integrity; status honest | adoption documented, no overclaim |

Each PR: validate before write, keep offline tests green, no secret, no overclaim,
one capability per PR.

### Build status (2026-06-03)

- **PR 2 — dry-run endpoint:** ✅ merged (`POST /api/ha/bindings/adopt`, dry-run only).
- **Revision store infrastructure** (the store half of PR 4): ✅ added —
  `IBindingsRevisionStore` + `InMemoryBindingsRevisionStore` +
  `SqliteBindingsRevisionStore` + `BindingsRevisionStoreOptions`
  (`BINDINGS_REVISION_STORE_PROVIDER`, default `memory`; SQLite at
  `data/bindings-revisions.sqlite`). Synchronous to match the existing store
  layer. **Not yet wired** into the adopt endpoint — no real write, no revision
  recorded yet. Recording + real file write land with PR 3/4.

---

## 7. Out of scope (for the whole adoption track, unless re-scoped)

- Hot-reload of bindings without restart (separate, larger change to `Factory`).
- Moving the runtime source of truth into the DB (Option B).
- Any MAPE-K / autonomous / real-HA-execution change.
- Editing the tracked demo profiles (`showcase`/`testlab`) from the UI.

---

## 8. Resolved decisions

All review questions are resolved — see **§0 Decisions (locked 2026-06-03)**:

1. Adopted file → `state-data/ha-bindings.runtime.json` (gitignored); never a tracked profile.
2. Reload → restart-required first slice; no hot-reload; `reloadRequired: true`.
3. Audit → dedicated `IBindingsRevisionStore` **+** `ISafetyEventLog` adoption event.
4. WARN → adoptable only with `acceptWarnings: true`; PASS direct; FAIL/invalid rejected.
5. Stale-write → mandatory `expectedCurrentHash`; mismatch → `409`; response returns `currentHash`/`adoptedHash`/`revisionId`.
6. Multi-profile → active runtime file only; `profile` is metadata; named multi-profile is future work.
