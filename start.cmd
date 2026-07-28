@echo off
chcp 65001 >nul
title DayLoop

echo.
echo ========================================
echo    DayLoop
echo ========================================
echo.
echo  1. Start server (local access)
echo  2. Start server + LAN access info
echo  3. Start server + public internet access (ngrok tunnel)
echo  4. Developer mode (auto reload)
echo  5. Build Android APK (needs Docker)
echo  6. Build iOS project (needs macOS)
echo  7. Exit
echo.

:menu
set /p choice=Select [1-7]:
if "%choice%"=="1" goto start_local
if "%choice%"=="2" goto start_lan
if "%choice%"=="3" goto start_public
if "%choice%"=="4" goto dev_mode
if "%choice%"=="5" goto build_android
if "%choice%"=="6" goto build_ios
if "%choice%"=="7" goto end
goto menu

:start_local
echo.
echo Building frontend...
cd /d "%~dp0frontend"
call npm run build
cd ..
echo Starting backend...
set TUNNEL=false
cd backend
npm start
goto end

:start_lan
echo.
echo Building frontend...
cd /d "%~dp0frontend"
call npm run build
cd ..
echo Starting backend with LAN info...
set TUNNEL=false
cd backend
npm start
goto end

:start_public
echo.
echo Building frontend...
cd /d "%~dp0frontend"
call npm run build
cd ..
echo.
echo Starting backend with public tunnel...
set TUNNEL=true
cd backend
npm start
goto end

:dev_mode
echo.
start "DayLoop Backend" cmd /c "cd /d %~dp0backend && node --watch src/index.js"
start "DayLoop Frontend" cmd /c "cd /d %~dp0frontend && npx vite"
echo Backend: http://localhost:3001
echo Frontend: http://localhost:5173
echo.
pause
goto end

:build_android
echo.
call "%~dp0scripts\docker-build-apk.cmd"
pause
goto end

:build_ios
echo.
echo iOS build requires macOS with Xcode.
echo.
echo Steps:
echo  1. cd frontend
echo  2. npm install ^&^& npm run build
echo  3. npm install @capacitor/core @capacitor/cli @capacitor/ios
echo  4. npx cap add ios
echo  5. npx cap open ios
echo  6. Build in Xcode
echo.
pause
goto end

:end
echo.
timeout /t 2 >nul
