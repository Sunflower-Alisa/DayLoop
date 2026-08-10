@echo off
chcp 65001 >nul
title DayLoop Service Installer

cd /d "%~dp0.."
set SCRIPTS_DIR=%~dp0

echo ============================================
echo  DayLoop Windows Service Installer
echo ============================================
echo.
echo This script will:
echo  1. Stop and remove existing DayLoop services (if any)
echo  2. Install Node.js backend as a Windows service (port 3001)
echo  3. Install .NET backend as a Windows service (port 5000)
echo  4. Start both services
echo.
echo Administrator privileges required!
echo If prompted, click Yes.
echo.

:: Check admin rights
net session >nul 2>&1
if %errorlevel% neq 0 (
  echo ERROR: Please run this script as Administrator!
  echo Right-click the script and select "Run as administrator".
  pause
  exit /b 1
)

:: Stop and remove existing services
echo [1/4] Stopping existing DayLoop services...
for /f "tokens=2 delims=," %%a in ('tasklist /FI "SERVICES eq DayLoop-Node" /FO CSV /NH 2^>nul') do (
  nssm stop DayLoop-Node >nul 2>&1
  nssm remove DayLoop-Node confirm >nul 2>&1
)
for /f "tokens=2 delims=," %%a in ('tasklist /FI "SERVICES eq DayLoop-DotNet" /FO CSV /NH 2^>nul') do (
  nssm stop DayLoop-DotNet >nul 2>&1
  nssm remove DayLoop-DotNet confirm >nul 2>&1
)

:: Kill any remaining backend processes
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":3001" ^| findstr "LISTENING"') do taskkill /F /PID %%a >nul 2>&1
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":5000" ^| findstr "LISTENING"') do taskkill /F /PID %%a >nul 2>&1

echo  Done

:: Install Node.js backend service
echo [2/4] Installing Node.js backend service (port 3001)...
nssm install DayLoop-Node "D:\Program Files\nodejs\node.exe" "D:\02.Personal\04.Code\DayLoop\backend\src\index.js"
nssm set DayLoop-Node AppDirectory "D:\02.Personal\04.Code\DayLoop\backend"
nssm set DayLoop-Node AppStdout "D:\02.Personal\04.Code\DayLoop\backend\logs\stdout.log"
nssm set DayLoop-Node AppStderr "D:\02.Personal\04.Code\DayLoop\backend\logs\stderr.log"
nssm set DayLoop-Node AppNoConsole 1
nssm set DayLoop-Node DisplayName "DayLoop Node.js Backend"
nssm set DayLoop-Node Description "DayLoop daily planner - Node.js backend on port 3001, also serves frontend static files"
nssm set DayLoop-Node Start SERVICE_AUTO_START
nssm set DayLoop-Node AppThrottle 1000
nssm set DayLoop-Node AppRestartDelay 5000
echo  Done

:: Install .NET backend service
echo [3/4] Installing .NET backend service (port 5000)...
nssm install DayLoop-DotNet "C:\Program Files\dotnet\dotnet.exe" "run --urls http://0.0.0.0:5000"
nssm set DayLoop-DotNet AppDirectory "D:\02.Personal\04.Code\DayLoop\backend-dotnet"
nssm set DayLoop-DotNet AppStdout "D:\02.Personal\04.Code\DayLoop\backend-dotnet\logs\stdout.log"
nssm set DayLoop-DotNet AppStderr "D:\02.Personal\04.Code\DayLoop\backend-dotnet\logs\stderr.log"
nssm set DayLoop-DotNet AppNoConsole 1
nssm set DayLoop-DotNet DisplayName "DayLoop .NET Backend"
nssm set DayLoop-DotNet Description "DayLoop daily planner - .NET backend on port 5000"
nssm set DayLoop-DotNet Start SERVICE_AUTO_START
nssm set DayLoop-DotNet AppThrottle 1000
nssm set DayLoop-DotNet AppRestartDelay 5000
echo  Done

:: Start services
echo [4/4] Starting services...
nssm start DayLoop-Node
nssm start DayLoop-DotNet

echo.
echo ============================================
echo  DayLoop services installed and started!
echo ============================================
echo.
echo  Status:
nssm status DayLoop-Node
nssm status DayLoop-DotNet
echo.
echo  Services will auto-start on boot.
echo.
echo  To check status, run: services.msc
echo  To view logs, check backend\logs\*.log
echo.
echo  To update after code changes:
echo    1. Run scripts\deploy-all.cmd (builds frontends)
echo    2. Then restart services: nssm restart DayLoop-Node / DayLoop-DotNet
echo.
pause