@echo off
chcp 65001 >nul
title DayLoop Server

cd /d "%~dp0.."

echo Starting DayLoop backends...
echo.

:: Kill existing instances
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":3001" ^| findstr "LISTENING"') do taskkill /F /PID %%a >nul 2>&1
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":5000" ^| findstr "LISTENING"') do taskkill /F /PID %%a >nul 2>&1

:: Start Node.js backend (port 3001) - also serves frontend static files
echo [1/2] Starting Node.js backend (port 3001)...
start "DayLoop Node Backend" cmd /c "cd /d %~dp0..\backend && node src/index.js"

:: Start .NET backend (port 5000)
echo [2/2] Starting .NET backend (port 5000)...
start "DayLoop .NET Backend" cmd /c "cd /d %~dp0..\backend-dotnet && dotnet run --urls http://0.0.0.0:5000"

timeout /t 5 /nobreak >nul

echo.
echo ============================================
echo  DayLoop is running!
echo ============================================
echo.
echo  Node.js backend: http://localhost:3001
echo  .NET backend:    http://localhost:5000
echo.
echo  This window will auto-close in 3 seconds...
timeout /t 3 /nobreak >nul