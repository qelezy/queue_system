#!/usr/bin/env bash
set -eu

/opt/mssql/bin/sqlservr &
SQL_PID=$!

cleanup() {
  kill "$SQL_PID" 2>/dev/null || true
}

if ! /scripts/restore-db.sh; then
  cleanup
  exit 1
fi

wait "$SQL_PID"
