@echo off
set PORT=3001
set JWT_SECRET=DayLoop-Default-Secret-Key-2026-Change-In-Production!
set DAYLOOP_SERVICE_TOKEN=DayLoop-Agent-Service-Token-2026
cd /d D:\02.Personal\04.Code\DayLoop
node backend\src\index.js
