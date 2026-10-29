@echo off
cd /d "%~dp0"
if exist "%~dp0NanjingPlanner.exe" (
  start "" "%~dp0NanjingPlanner.exe"
  exit /b
)
echo This folder contains source code, not the Windows runtime.
echo Download the Windows ZIP from the release page and extract all files.
start "" "https://github.com/YuuKldesu/Trip-Agent/releases/latest"
pause
