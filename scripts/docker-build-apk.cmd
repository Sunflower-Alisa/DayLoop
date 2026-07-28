@echo off
chcp 65001 >nul
echo ========================================
echo  DayLoop Android APK Builder (Docker)
echo ========================================
echo.
echo Prerequisites: Docker Desktop for Windows
echo.

cd /d "%~dp0.."

echo [1/4] Building frontend...
cd frontend
call npm run build
if %errorlevel% neq 0 exit /b %errorlevel%
cd ..

echo [2/4] Building Docker Android builder image...
docker build -t dayloop-android-builder -f docker\android-builder\Dockerfile .
if %errorlevel% neq 0 (
  echo Docker build failed. Is Docker installed?
  pause
  exit /b %errorlevel%
)

echo [3/4] Extracting APK from Docker container...
docker create --name dayloop-temp dayloop-android-builder
docker cp dayloop-temp:/app/output/DayLoop.apk .\DayLoop.apk
docker rm dayloop-temp

echo [4/4] Done!
echo.
echo ========================================
echo  APK generated: DayLoop.apk
echo  Size: 
for %%I in (DayLoop.apk) do echo  %%~zI bytes
echo ========================================
echo.
echo  Install on Android device:
echo  1. Copy DayLoop.apk to your phone
echo  2. Enable "未知来源" in settings
echo  3. Open the APK file to install
echo.
pause
