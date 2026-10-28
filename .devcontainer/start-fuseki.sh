#!/bin/sh
# Starts Fuseki on localhost:3030 unless it is already running.
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RUN_DIR=/tmp/fuseki
mkdir -p "$RUN_DIR"
if curl -sf http://localhost:3030/$/ping >/dev/null 2>&1; then exit 0; fi
sed "s|@RULES_FILE@|$ROOT/models-and-rules/inference-rules.rules|;s|@DB_DIR@|$RUN_DIR/db|" "$ROOT/.devcontainer/fuseki/config.ttl" > "$RUN_DIR/config.ttl"
PATH=/opt/java21/bin:$PATH FUSEKI_BASE="$RUN_DIR" nohup /opt/fuseki/fuseki-server --localhost --port=3030 --config="$RUN_DIR/config.ttl" > "$RUN_DIR/fuseki.log" 2>&1 &
