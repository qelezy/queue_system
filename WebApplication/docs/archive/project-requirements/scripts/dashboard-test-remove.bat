@echo off
setlocal
set "SERVER=localhost\SQLEXPRESS01"
set "DB=ElectronicQueueProf"
cd /d "%~dp0"

echo === Dashboard test remove (tolko testovye talony) ===

sqlcmd -S "%SERVER%" -d "%DB%" -E -i dashboard-test-rollback.sql
if errorlevel 1 goto :fail

echo.
echo Gotovo: testovye talony udaleny (sm. remaining_test_appointments / test_on_today_msk v vyvode)
exit /b 0

:fail
echo.
echo Oshibka. Proverte sqlcmd i podklyuchenie k %SERVER% / %DB%
exit /b 1
