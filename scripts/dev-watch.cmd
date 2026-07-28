@echo off
chcp 65001 >nul
echo ========================================
echo  DayLoop Developer Watch Mode
echo ========================================
echo.
echo  This will auto-rebuild frontend on changes
echo.  and restart backend on changes.
echo.
echo  Press Ctrl+C to stop
echo.

cd /d "%~dp0.."

echo [1/3] Starting backend in watch mode...
start "DayLoop Backend" cmd /c "cd backend && node --watch src/index.js"

echo [2/3] Starting frontend build watcher...
echo.
:watch_loop
cd frontend
echo Watching for changes... (every 3 seconds)
:loop
timeout /t 3 /nobreak >nul

:: Check if any .vue, .ts files changed in src/
for /f %%i in ('dir /s /b src\*.vue src\*.ts 2^>nul') do (
  if exist "%%i" (
    echo Change detected in %%i
    echo Rebuilding frontend...
    call npm run build
    echo Build complete at %time%
    echo.
    goto loop
  )
)
goto loop
