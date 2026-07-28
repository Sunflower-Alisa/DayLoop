@echo off
chcp 65001 >nul
title DayLoop Deploy

cd /d "%~dp0.."

echo ========================================
echo  DayLoop Deploy - Build ^& Restart
echo ========================================
echo.

:: Step 1: Build frontend (Node.js version)
echo [1/4] Building frontend (Node.js)...
pushd frontend
call npm run build
if errorlevel 1 (
  popd
  echo ERROR: Frontend (Node.js) build failed!
  pause
  exit /b 1
)
popd

:: Step 2: Build frontend-dotnet
echo [2/4] Building frontend (.NET)...
pushd frontend-dotnet
call npm run build
if errorlevel 1 (
  popd
  echo ERROR: Frontend (.NET) build failed!
  pause
  exit /b 1
)
popd

:: Step 3: Kill existing backend processes
echo [3/4] Stopping existing backends...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":3001" ^| findstr "LISTENING"') do (
  taskkill /F /PID %%a >nul 2>&1
  echo  Killed Node.js backend (PID %%a)
)
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":5000" ^| findstr "LISTENING"') do (
  taskkill /F /PID %%a >nul 2>&1
  echo  Killed .NET backend (PID %%a)
)

:: Step 4: Start backends
echo [4/4] Starting backends...
start "DayLoop Node Backend" cmd /c "cd /d %~dp0..\backend && node src/index.js"
start "DayLoop .NET Backend" cmd /c "cd /d %~dp0..\backend-dotnet && dotnet run --urls http://0.0.0.0:5000"

timeout /t 5 /nobreak >nul

echo.
echo ============================================
echo  DayLoop deployed successfully!
echo ============================================
echo.
echo  Node.js backend: http://localhost:3001
echo  .NET backend:    http://localhost:5000
echo.
echo  Press any key to close...
pause >nul