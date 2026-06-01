#!/usr/bin/env bash
set -euo pipefail

MSSQL_HOST="${MSSQL_HOST:-mssql}"
BACKUP_DIR="${BACKUP_DIR:-/backups}"
SQL_DIR="${SQL_DIR:-/scripts/sql}"
DATA_DIR="${MSSQL_DATA_DIR:-/var/opt/mssql/data}"
SQLCMD=(/opt/mssql-tools18/bin/sqlcmd -S "$MSSQL_HOST" -U sa -P "${MSSQL_SA_PASSWORD}" -C)

if [[ -f /scripts/restore.env ]]; then
  set -a
  # shellcheck source=/dev/null
  source /scripts/restore.env
  set +a
fi

USER_DB_NAME="${USER_DB_NAME:-UserDb}"
USER_BAK_FILE="${USER_BAK_FILE:-UserDb.bak}"
QUEUE_DB_NAME="${QUEUE_DB_NAME:-ElectronicQueueProf}"
QUEUE_BAK_FILE="${QUEUE_BAK_FILE:-ElectronicQueueProf.bak}"

USER_BAK_PATH="${BACKUP_DIR}/${USER_BAK_FILE}"
QUEUE_BAK_PATH="${BACKUP_DIR}/${QUEUE_BAK_FILE}"

wait_for_sql() {
  echo "Waiting for SQL Server at ${MSSQL_HOST}..."
  for _ in $(seq 1 60); do
    if "${SQLCMD[@]}" -Q "SELECT 1" &>/dev/null; then
      echo "SQL Server is ready."
      return 0
    fi
    sleep 2
  done
  echo "ERROR: SQL Server did not become ready in time."
  exit 1
}

database_exists() {
  local db="$1"
  local escaped="${db//\'/\'\'}"
  local count
  count=$("${SQLCMD[@]}" -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = N'${escaped}'" | tr -d '[:space:]')
  [[ "$count" == "1" ]]
}

restore_database() {
  local db_name="$1"
  local bak_path="$2"

  if database_exists "$db_name"; then
    echo "Database [${db_name}] already exists — skip restore."
    return 0
  fi

  if [[ ! -f "$bak_path" ]]; then
    echo "ERROR: Backup file not found: ${bak_path}"
    exit 1
  fi

  echo "Restoring [${db_name}] from [${bak_path}]..."

  local move_clauses=""
  local data_idx=0
  local log_idx=0

  while IFS='|' read -r logical_name _physical_name file_type _rest; do
    logical_name=$(echo "$logical_name" | xargs)
    file_type=$(echo "$file_type" | xargs)
    [[ -z "$logical_name" || "$logical_name" == "LogicalName" ]] && continue

    if [[ "$file_type" == "D" ]]; then
      local target
      if [[ "$data_idx" -eq 0 ]]; then
        target="${DATA_DIR}/${db_name}.mdf"
      else
        target="${DATA_DIR}/${db_name}_${data_idx}.mdf"
      fi
      move_clauses+=", MOVE N'${logical_name}' TO N'${target}'"
      data_idx=$((data_idx + 1))
    elif [[ "$file_type" == "L" ]]; then
      local target
      if [[ "$log_idx" -eq 0 ]]; then
        target="${DATA_DIR}/${db_name}_log.ldf"
      else
        target="${DATA_DIR}/${db_name}_log_${log_idx}.ldf"
      fi
      move_clauses+=", MOVE N'${logical_name}' TO N'${target}'"
      log_idx=$((log_idx + 1))
    fi
  done < <("${SQLCMD[@]}" -s"|" -W -h -1 -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'${bak_path}'")

  if [[ -z "$move_clauses" ]]; then
    echo "ERROR: Could not read file list from backup: ${bak_path}"
    exit 1
  fi

  local escaped_bak="${bak_path//\'/\'\'}"
  "${SQLCMD[@]}" -Q "RESTORE DATABASE [${db_name}] FROM DISK = N'${escaped_bak}' WITH REPLACE${move_clauses}"
  echo "Restored [${db_name}]."
}

run_sql_bootstrap() {
  echo "No .bak files found — applying SQL dev bootstrap from ${SQL_DIR}..."
  if [[ ! -d "$SQL_DIR" ]]; then
    echo "ERROR: SQL directory not found: ${SQL_DIR}"
    exit 1
  fi
  local script
  shopt -s nullglob
  local scripts=("$SQL_DIR"/*.sql)
  shopt -u nullglob
  if [[ ${#scripts[@]} -eq 0 ]]; then
    echo "ERROR: No SQL scripts in ${SQL_DIR}"
    exit 1
  fi
  IFS=$'\n' scripts=($(printf '%s\n' "${scripts[@]}" | sort))
  unset IFS
  for script in "${scripts[@]}"; do
    echo "Running $(basename "$script")..."
    "${SQLCMD[@]}" -b -i "$script"
  done
  echo "SQL dev bootstrap complete."
}

has_user_bak=false
has_queue_bak=false
[[ -f "$USER_BAK_PATH" ]] && has_user_bak=true
[[ -f "$QUEUE_BAK_PATH" ]] && has_queue_bak=true

wait_for_sql

if $has_user_bak && $has_queue_bak; then
  echo "Found both backup files — restoring from .bak..."
  restore_database "$USER_DB_NAME" "$USER_BAK_PATH"
  restore_database "$QUEUE_DB_NAME" "$QUEUE_BAK_PATH"
elif $has_user_bak || $has_queue_bak; then
  echo "ERROR: Found only one backup file. Provide both ${USER_BAK_FILE} and ${QUEUE_BAK_FILE}, or none for SQL bootstrap."
  exit 1
else
  run_sql_bootstrap
fi

echo "Database initialization complete."
