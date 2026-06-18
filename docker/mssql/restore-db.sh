#!/usr/bin/env bash
set -eu

BACKUP_DIR="/backups"
DATA_DIR="/var/opt/mssql/data"
TARGET_COMPATIBILITY_LEVEL=160
SQLCMD=(/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "${MSSQL_SA_PASSWORD}" -C -b)

USER_BAK_PATH="${BACKUP_DIR}/UserDb.bak"
QUEUE_BAK_PATH="${BACKUP_DIR}/ElectronicQueueProf.bak"

run_sqlcmd() {
  if ! "${SQLCMD[@]}" "$@"; then
    echo "ERROR: sqlcmd failed."
    exit 1
  fi
}

escape_sql_literal() {
  local value="$1"
  echo "${value//\'/\'\'}"
}

require_both_backups() {
  local missing=()
  [[ -f "$USER_BAK_PATH" ]] || missing+=("UserDb.bak")
  [[ -f "$QUEUE_BAK_PATH" ]] || missing+=("ElectronicQueueProf.bak")
  if [[ ${#missing[@]} -eq 0 ]]; then
    return 0
  fi
  echo "ERROR: Missing backup file(s) in ${BACKUP_DIR}: ${missing[*]}"
  echo "Place UserDb.bak and ElectronicQueueProf.bak in docker/backups/ before docker compose up."
  exit 1
}

wait_for_sql() {
  echo "Waiting for SQL Server..."
  local probe=(/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "${MSSQL_SA_PASSWORD}" -C)
  for _ in $(seq 1 60); do
    if "${probe[@]}" -Q "SELECT 1" &>/dev/null; then
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
  local escaped
  escaped=$(escape_sql_literal "$db")
  local count
  count=$(run_sqlcmd -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = N'${escaped}'" | tr -d '[:space:]')
  [[ "$count" == "1" ]]
}

finalize_database() {
  local db_name="$1"
  local escaped
  escaped=$(escape_sql_literal "$db_name")
  echo "Finalizing [${db_name}]..."
  run_sqlcmd -Q "
SET NOCOUNT ON;
IF DB_ID(N'${escaped}') IS NULL
BEGIN
  RAISERROR(N'Database not found after restore.', 16, 1);
  RETURN;
END
DECLARE @level INT = (SELECT compatibility_level FROM sys.databases WHERE name = N'${escaped}');
IF @level < ${TARGET_COMPATIBILITY_LEVEL}
  EXEC(N'ALTER DATABASE [${db_name}] SET COMPATIBILITY_LEVEL = ${TARGET_COMPATIBILITY_LEVEL}');
ALTER DATABASE [${db_name}] SET MULTI_USER;
IF (SELECT state_desc FROM sys.databases WHERE name = N'${escaped}') <> N'ONLINE'
  ALTER DATABASE [${db_name}] SET ONLINE;
"
}

restore_database() {
  local db_name="$1"
  local bak_path="$2"
  local escaped_bak
  escaped_bak=$(escape_sql_literal "$bak_path")

  if database_exists "$db_name"; then
    echo "Database [${db_name}] already exists — skip restore."
    return 0
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
  done < <(run_sqlcmd -s"|" -W -h -1 -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'${escaped_bak}'")

  if [[ -z "$move_clauses" ]]; then
    echo "ERROR: Could not read file list from backup: ${bak_path}"
    exit 1
  fi

  run_sqlcmd -Q "RESTORE DATABASE [${db_name}] FROM DISK = N'${escaped_bak}' WITH REPLACE, RECOVERY, STATS = 10${move_clauses}"
  echo "Restored [${db_name}]."
  finalize_database "$db_name"
}

wait_for_sql

if database_exists "UserDb" && database_exists "ElectronicQueueProf"; then
  echo "Databases already exist — skip restore."
else
  require_both_backups
  echo "Restoring databases from backup files..."
  restore_database "UserDb" "$USER_BAK_PATH"
  restore_database "ElectronicQueueProf" "$QUEUE_BAK_PATH"
fi

echo "Database initialization complete."
