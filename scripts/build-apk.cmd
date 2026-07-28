@echo off
echo ========================================
echo  DayLoop Android APK Builder
echo ========================================
echo.
echo Prerequisites:
echo   - Node.js >= 22
echo   - Android Studio with Android SDK
echo   - Java JDK >= 17
echo.

cd /d "%~dp0..\frontend"

echo [1/5] Installing dependencies...
call npm install
if %errorlevel% neq 0 exit /b %errorlevel%

echo [2/5] Building frontend...
call npm run build
if %errorlevel% neq 0 exit /b %errorlevel%

echo [3/5] Installing Capacitor...
call npm install @capacitor/core @capacitor/cli @capacitor/android
if %errorlevel% neq 0 exit /b %errorlevel%

echo [4/5] Adding Android platform...
npx cap add android
if %errorlevel% neq 0 exit /b %errorlevel%

echo [5/5] Building APK...
npx cap copy android
cd android
call gradlew assembleDebug
if %errorlevel% neq 0 exit /b %errorlevel%

echo.
echo ========================================
echo  APK generated at:
echo  frontend/android/app/build/outputs/apk/debug/app-debug.apk
echo ========================================
pause
