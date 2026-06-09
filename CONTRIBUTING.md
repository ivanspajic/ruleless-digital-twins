# Contributing to Ruleless Digital Twins

Thanks for your interest in contributing! This document explains how to work on this repository.

## Project overview

Ruleless digital twins for smart environments, built on a hybrid MAPE-K architecture:

- **SmartNode** — .NET 8 / C# MAPE-K controller.
- **Inference engine** — Java (`ruleless-digital-twins-inference-engine.jar`).
- **Ontology & rules** — OWL/Turtle (SOSA/SSN) + Apache Jena rules.
- **Integration** — Home Assistant, RabbitMQ, MongoDB, FMU/OpenModelica.

## Getting started

1. Clone the repository.
2. Build the SmartNode solution:
   ```bash
   dotnet build ./SmartNode/SmartNode.sln --nologo
   ```
3. Run the tests:
   ```bash
   dotnet test ./SmartNode/SmartNode.sln --nologo
   ```

## Branching model

`main` is protected: **never commit directly to `main`**, and never rewrite
published history. Changes reach `main` only through a pull request after the
task-scoped diff has been reviewed and validation has passed.

Use these branch prefixes:

| Prefix | Purpose |
|--------|---------|
| `feature/*` | New features |
| `fix/*` | Bug fixes |
| `chore/*` | Maintenance, tooling, cleanup |
| `docs/*` | Documentation work |
| `refactor/*` | Refactoring |
| `test/*` | Test-only changes |

Do not mix unrelated work in a single branch.

## Before opening a pull request

Run the following and make sure they pass:

```bash
git diff --check
dotnet build ./SmartNode/SmartNode.sln --nologo
dotnet test ./SmartNode/SmartNode.sln --nologo
```

Documentation-only changes may skip the build, but say so explicitly in the PR.

## Code conventions

- Favor short, testable methods. Separate business logic, data access (MongoDB, RabbitMQ), and Home Assistant integration.
- Every new sensor/actuator must be registered in `SmartNode/Factory.cs`.
- Keep OWL/Turtle files syntactically valid (prefixes, punctuation, XSD types).
- Keep the mapping between Home Assistant entity names and OWL individuals exact and consistent.

## Secrets

Never commit secrets or runtime state: access tokens, real `.env` files, Home Assistant `.storage/` or databases, logs, `*.token`, `secrets.json`, generated inferred TTL files, or build output (`bin/`, `obj/`). `.env.example` is allowed only with clearly fake placeholder values.

## Pull request checklist

- [ ] Branch follows the naming model above.
- [ ] Validation commands pass (or the failure is explained).
- [ ] No secrets or runtime state included.
- [ ] The diff is limited to a single, focused change.
