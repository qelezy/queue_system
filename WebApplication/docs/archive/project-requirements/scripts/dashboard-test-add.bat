@echo off
setlocal
set "SERVER=localhost\SQLEXPRESS01"
set "DB=ElectronicQueueProf"
cd /d "%~dp0"

echo === Dashboard test add (rollback + seed + verify) ===

sqlcmd -S "%SERVER%" -d "%DB%" -E -i dashboard-test-rollback.sql
if errorlevel 1 goto :fail

sqlcmd -S "%SERVER%" -d "%DB%" -E -i dashboard-test-seed.sql
if errorlevel 1 goto :fail

sqlcmd -S "%SERVER%" -d "%DB%" -E -i dashboard-test-verify-today.sql
if errorlevel 1 goto :fail

echo.
echo Gotovo: 8 talonov na segodnya MSK, 0 min ozhidaniya/priema na /dashboard
exit /b 0

:fail
echo.
echo Oshibka. Proverte sqlcmd i podklyuchenie k %SERVER% / %DB%
exit /b 1
