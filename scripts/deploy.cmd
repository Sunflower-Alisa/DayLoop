@echo off
echo ========================================
echo  DayLoop Production Deploy
echo ========================================
echo.

cd /d "%~dp0.."

echo [1/4] Building frontend for production...
cd frontend
call npm run build
if %errorlevel% neq 0 exit /b %errorlevel%
cd ..

echo [2/4] Generating icons...
cd frontend
node scripts/generate-icons.cjs
cd ..

echo [3/4] Starting backend server...
start "DayLoop Backend" cmd /c "cd backend && npm start"

timeout /t 3 /nobreak >nul

echo.
echo ========================================
echo  DayLoop is running!
echo ========================================
echo.
echo  Local access:     http://localhost:3001
echo.
echo  To expose to internet, choose one:
echo.
echo  Option A - Use ngrok (recommended):
echo    1. Download ngrok from: https://ngrok.com/download
echo    2. Run: ngrok http 3001
echo    3. Access your app at the ngrok URL
echo.
echo  Option B - Use Cloudflare Tunnel:
echo    1. Install cloudflared
echo    2. Run: cloudflared tunnel --url http://localhost:3001
echo.
echo  Option C - Port forwarding:
echo    1. Open router admin panel (192.168.1.1)
echo    2. Forward port 3001 to this computer's IP
echo    3. Access via your public IP address
echo.
echo ========================================
echo  PWA is installable on:
echo    Android Chrome: Menu -> Add to Home screen
echo    iOS Safari:     Share -> Add to Home Screen
echo ========================================
echo.
echo  Press Ctrl+C to stop
pause
