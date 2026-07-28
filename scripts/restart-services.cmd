@echo off
chcp 65001 >nul
title DayLoop Service Restart

cd /d "%~dp0.."

net session >nul 2>&1
if %errorlevel% neq 0 (
  echo ERROR: Please run as Administrator!
  pause
  exit /b 1
)

echo Restarting DayLoop services...
nssm restart DayLoop-Node
nssm restart DayLoop-DotNet
echo.
echo Services restarted!
pause