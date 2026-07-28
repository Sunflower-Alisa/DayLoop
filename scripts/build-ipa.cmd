@echo off
echo ========================================
echo  DayLoop iOS IPA Builder (macOS only)
echo ========================================
echo.
echo Prerequisites:
echo   - macOS with Xcode >= 15
echo   - Node.js >= 22
echo   - Apple Developer account
echo.
echo NOTE: iOS builds can only be done on macOS.
echo This script will set up the iOS project on macOS.
echo.

cd /d "%~dp0..\frontend"

echo [1/5] Installing dependencies...
call npm install
if %errorlevel% neq 0 exit /b %errorlevel%

echo [2/5] Building frontend...
call npm run build
if %errorlevel% neq 0 exit /b %errorlevel%

echo [3/5] Installing Capacitor...
call npm install @capacitor/core @capacitor/cli @capacitor/ios
if %errorlevel% neq 0 exit /b %errorlevel%

echo [4/5] Adding iOS platform...
npx cap add ios
if %errorlevel% neq 0 exit /b %errorlevel%

echo [5/5] Opening in Xcode...
npx cap open ios

echo.
echo ========================================
echo  iOS project opened in Xcode
echo  Build from Xcode to generate IPA
echo ========================================
pause
