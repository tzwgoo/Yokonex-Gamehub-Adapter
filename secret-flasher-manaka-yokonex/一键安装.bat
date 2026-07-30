@echo off
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
if errorlevel 1 echo 安装失败，请按上方提示检查游戏目录和 BepInEx。
pause
